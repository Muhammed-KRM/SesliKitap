"""Bölüm 4.8 HTTP TTS sözleşmesini çağıran uzak GPU TTS istemcisi (Modal / Kaggle / Colab)."""

from __future__ import annotations

import base64
import time
from io import BytesIO

import httpx
import numpy as np
import soundfile as sf

from models.voice_models import (
    BatchResult,
    EngineInfo,
    PreparedVoice,
    SynthChunkInput,
    SynthChunkOutput,
    VoiceParams,
)
from pipeline.tts.base_engine import BaseTtsEngine, EngineUnavailableError
from utils.logger import get_logger

logger = get_logger(__name__)


class RemoteHttpTtsEngine(BaseTtsEngine):
    """Uzak HTTP TTS sunucusuna (voxcpm2_modal, voxcpm2_alt, xtts2_modal) bağlanan motor."""

    def __init__(
        self,
        *,
        engine_id: str,
        family: str,
        base_url: str | None,
        api_token: str | None,
        request_timeout_sec: int = 600,
        cold_start_timeout_sec: int = 300,
        max_retries: int = 3,
    ) -> None:
        self.engine_id = engine_id
        self.family = family
        self._base_url = (base_url or "").strip().rstrip("/")
        self._api_token = (api_token or "").strip()
        self._request_timeout_sec = max(5, request_timeout_sec)
        self._cold_start_timeout_sec = max(5, cold_start_timeout_sec)
        self._max_retries = max(1, max_retries)
        self._cached_info: EngineInfo | None = None
        self._client: httpx.Client | None = None

    def is_available(self) -> bool:
        if not self._base_url or not self._api_token:
            return False
        return "<workspace>" not in self._base_url.lower() and "degistir_beni" not in self._api_token.lower()

    def _get_client(self) -> httpx.Client:
        if self._client is None:
            self._client = httpx.Client(
                timeout=httpx.Timeout(
                    connect=10.0,
                    read=float(self._request_timeout_sec),
                    write=60.0,
                    pool=10.0,
                ),
                headers={"Authorization": f"Bearer {self._api_token}"},
            )
        return self._client

    def close(self) -> None:
        if self._client is not None:
            self._client.close()
            self._client = None

    def info(self) -> EngineInfo:
        if self._cached_info is not None:
            return self._cached_info

        if not self.is_available():
            raise EngineUnavailableError(self.engine_id, "Motor URL veya API token yapılandırılmamış.")

        client = self._get_client()
        try:
            resp = client.get(
                f"{self._base_url}/v1/info",
                timeout=float(self._cold_start_timeout_sec),
            )
            resp.raise_for_status()
            data = resp.json()
            self._cached_info = EngineInfo(
                engine=str(data.get("engine", self.family)),
                model_id=str(data.get("modelId", self.family)),
                model_revision=str(data.get("modelRevision", "v1")),
                sample_rate=int(data.get("sampleRate", 24000)),
                max_batch=int(data.get("maxBatch", data.get("maxBatchItems", 16))),
                max_chars_per_chunk=int(data.get("maxCharsPerChunk", data.get("maxTextChars", 600))),
            )
            return self._cached_info
        except httpx.HTTPError as exc:
            raise EngineUnavailableError(self.engine_id, f"/v1/info hatası: {exc}") from exc

    def has_voice(self, voice_hash: str) -> bool:
        client = self._get_client()
        try:
            resp = client.head(f"{self._base_url}/v1/voices/{voice_hash}")
            if resp.status_code == 200:
                return True
            if resp.status_code == 404:
                return False
            resp.raise_for_status()
            return False
        except httpx.HTTPError as exc:
            raise EngineUnavailableError(self.engine_id, f"HEAD /v1/voices hatası: {exc}") from exc

    def register_voice(self, voice: PreparedVoice) -> None:
        client = self._get_client()
        raw_bytes = voice.wav_path.read_bytes()
        payload = {
            "audioB64": base64.b64encode(raw_bytes).decode("ascii"),
            "promptText": voice.prompt_text,
            "sampleRate": 24000,
        }
        try:
            resp = client.put(
                f"{self._base_url}/v1/voices/{voice.voice_hash}",
                json=payload,
            )
            if resp.status_code in (200, 201):
                return
            resp.raise_for_status()
        except httpx.HTTPError as exc:
            raise EngineUnavailableError(self.engine_id, f"PUT /v1/voices hatası: {exc}") from exc

    def synthesize(
        self,
        chunks: list[SynthChunkInput],
        voice: PreparedVoice,
        params: VoiceParams,
        *,
        request_id: str = "req-default",
        language: str = "tr",
    ) -> BatchResult:
        if not self.is_available():
            raise EngineUnavailableError(self.engine_id, "Motor kullanılamıyor (yapılandırılmamış).")

        client = self._get_client()
        body = {
            "requestId": request_id,
            "engine": self.family,
            "voiceHash": voice.voice_hash,
            "language": language,
            "outputFormat": "flac",
            "output": {"codec": "flac", "sampleRate": 24000},
            "params": {
                "cfgValue": params.cfg_value,
                "inferenceTimesteps": params.inference_timesteps,
                "seed": params.seed,
                "normalizeText": False,
                "denoisePrompt": False,
            },
            "chunks": [
                {"id": c.id, "text": c.text, "seed": c.seed if c.seed is not None else params.seed}
                for c in chunks
            ],
        }

        voice_retried = False
        last_exc: Exception | None = None

        for attempt in range(1, self._max_retries + 1):
            try:
                resp = client.post(f"{self._base_url}/v1/synthesize", json=body)

                # 404 voice_not_cached -> sesi yükle ve hemen tekrar dene
                if resp.status_code == 404 and not voice_retried:
                    err_code = (resp.json().get("error") or {}).get("code", "")
                    if err_code == "voice_not_cached":
                        logger.info(
                            "Ses sunucu önbelleğinde yok (%s), yeniden yükleniyor: %s",
                            self.engine_id,
                            voice.voice_hash[:12],
                        )
                        self.register_voice(voice)
                        voice_retried = True
                        resp = client.post(f"{self._base_url}/v1/synthesize", json=body)

                if resp.status_code == 429:
                    raise EngineUnavailableError(
                        self.engine_id,
                        "Sunucu meşgul veya kota sınırına ulaşıldı (HTTP 429).",
                        quota_exhausted=True,
                    )

                if resp.status_code >= 500:
                    raise httpx.HTTPStatusError(
                        f"Sunucu hatası HTTP {resp.status_code}",
                        request=resp.request,
                        response=resp,
                    )

                resp.raise_for_status()
                data = resp.json()
                return self._parse_batch_response(data, attempt)
            except EngineUnavailableError:
                raise
            except (httpx.TimeoutException, httpx.TransportError, httpx.HTTPStatusError) as exc:
                last_exc = exc
                logger.warning(
                    "TTS isteği başarısız (%s, requestId=%s, deneme=%d/%d): %s",
                    self.engine_id,
                    request_id,
                    attempt,
                    self._max_retries,
                    exc,
                )
                if attempt < self._max_retries:
                    backoff = min(15.0, 0.5 * (2 ** (attempt - 1)))
                    time.sleep(backoff)

        raise EngineUnavailableError(
            self.engine_id,
            f"{self._max_retries} deneme sonunda sentez başarısız oldu: {last_exc}",
        ) from last_exc

    def _parse_batch_response(self, data: dict, attempt: int) -> BatchResult:
        outputs: list[SynthChunkOutput] = []
        failed_ids: list[str] = []
        default_sr = int(data.get("sampleRate", 24000))

        for item in data.get("results", []):
            cid = str(item["id"])
            ok = bool(item.get("ok", True))
            audio_b64 = item.get("audioB64")
            if not ok or not audio_b64:
                failed_ids.append(cid)
                continue

            raw_bytes = base64.b64decode(audio_b64)
            samples, sr = sf.read(BytesIO(raw_bytes), dtype="float32", always_2d=False)
            arr = np.asarray(samples, dtype=np.float32)
            if arr.ndim == 2:
                arr = arr.mean(axis=1)

            dur_ms = int(item.get("durationMs") or round((arr.size / float(sr or default_sr)) * 1000.0))
            outputs.append(
                SynthChunkOutput(
                    chunk_id=cid,
                    samples=arr,
                    sample_rate=int(sr or default_sr),
                    duration_ms=dur_ms,
                    attempts=int(item.get("attempts", attempt)),
                    engine_id=self.engine_id,
                    from_cache=False,
                )
            )

        gpu_ms = int(data.get("gpuMs", 0))
        logger.info(
            "TTS partisi alındı: engine=%s, requestId=%s, ok=%d, failed=%d, gpuMs=%d",
            self.engine_id,
            data.get("requestId", "-"),
            len(outputs),
            len(failed_ids),
            gpu_ms,
        )
        return BatchResult(
            engine_id=self.engine_id,
            family=self.family,
            model_revision=str(data.get("modelRevision", "v1")),
            gpu_ms=gpu_ms,
            outputs=outputs,
            failed_ids=failed_ids,
        )
