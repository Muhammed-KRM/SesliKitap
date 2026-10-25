"""Yerel TTS motoru (GPU sunucusu yokken gerçek Türkçe konuşma veya birim test sinyali üretir — Bölüm 4.15.6)."""

from __future__ import annotations

import hashlib
import io
import math
from concurrent.futures import ThreadPoolExecutor

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
from pipeline.tts.base_engine import BaseTtsEngine
from utils.logger import get_logger

logger = get_logger(__name__)


class MockTtsEngine(BaseTtsEngine):
    """Yalnızca allow_mock=True olduğunda etkinleşen yerel TTS motoru."""

    engine_id = "mock"
    family = "mock"

    def __init__(
        self,
        *,
        allow_mock: bool = False,
        sample_rate: int = 24000,
        use_speech: bool = False,
    ) -> None:
        self._allow_mock = allow_mock
        self._sample_rate = sample_rate
        self._use_speech = use_speech
        self._registered_voices: set[str] = set()

    @property
    def _model_revision(self) -> str:
        return "gtts-tr-1.0.0" if self._use_speech else "mock-1.0.0"

    def is_available(self) -> bool:
        return self._allow_mock

    def info(self) -> EngineInfo:
        return EngineInfo(
            engine="mock",
            model_id="seslikitap/local-turkish-tts-v1" if self._use_speech else "seslikitap/mock-worker-v1",
            model_revision=self._model_revision,
            sample_rate=self._sample_rate,
            max_batch=32,
            max_chars_per_chunk=600,
        )

    def has_voice(self, voice_hash: str) -> bool:
        return voice_hash.lower() in self._registered_voices

    def register_voice(self, voice: PreparedVoice) -> None:
        self._registered_voices.add(voice.voice_hash.lower())

    def _synthesize_gtts_single(self, text: str, language: str) -> np.ndarray | None:
        """gTTS ile gerçek Türkçe konuşma sesi üretir ve 24 kHz float32 dizisi döndürür."""
        try:
            from gtts import gTTS

            clean = text.strip()
            if not clean:
                return None

            buf = io.BytesIO()
            lang_code = (language or "tr").split("-")[0].lower() or "tr"
            gTTS(text=clean, lang=lang_code).write_to_fp(buf)
            buf.seek(0)
            samples, sr = sf.read(buf, dtype="float32", always_2d=False)
            arr = np.asarray(samples, dtype=np.float32)
            if arr.ndim == 2:
                arr = arr.mean(axis=1)

            if sr != self._sample_rate and arr.size > 0 and sr > 0:
                target_len = max(1, round(arr.size * self._sample_rate / float(sr)))
                x_old = np.linspace(0.0, 1.0, arr.size, endpoint=False)
                x_new = np.linspace(0.0, 1.0, target_len, endpoint=False)
                arr = np.interp(x_new, x_old, arr).astype(np.float32)

            nz = np.flatnonzero(np.abs(arr) > 0.008)
            if nz.size > 0:
                pad = int(self._sample_rate * 0.04)
                start = max(0, int(nz[0]) - pad)
                end = min(arr.size, int(nz[-1]) + pad)
                arr = arr[start:end]

            peak = float(np.max(np.abs(arr))) if arr.size > 0 else 0.0
            if peak > 1e-4:
                arr = (arr / peak * 0.65).astype(np.float32)

            # Çok kısa kelimelerde QualityChecker oranını korumak için minimum uzunluk kontrolü
            min_samples = max(int(self._sample_rate * 0.35), int(len(clean) * 0.032 * self._sample_rate))
            if arr.size < min_samples and arr.size > 0:
                repeats = max(1, math.ceil(min_samples / arr.size))
                if repeats == 1:
                    arr = np.pad(arr, (0, min_samples - arr.size))

            return arr
        except Exception as exc:
            logger.warning("Yerel gTTS sentezi başarısız oldu, sentetik sinyale dönülüyor: %s", exc)
            return None

    def _synthesize_tone_single(
        self,
        chunk: SynthChunkInput,
        voice: PreparedVoice,
        params: VoiceParams,
    ) -> np.ndarray:
        clean_len = max(8, len(chunk.text.strip()))
        duration_sec = max(0.65, min(25.0, clean_len * 0.065))
        n_samples = round(duration_sec * self._sample_rate)

        seed_val = chunk.seed if chunk.seed is not None else (params.seed or 0)
        digest = hashlib.sha256(f"{voice.voice_hash}:{chunk.text}:{seed_val}".encode()).digest()
        freq = 165.0 + (digest[0] % 80)

        t = np.linspace(0.0, duration_sec, n_samples, endpoint=False, dtype=np.float32)
        carrier = (
            0.55 * np.sin(2.0 * math.pi * freq * t)
            + 0.25 * np.sin(2.0 * math.pi * (freq * 2.0) * t)
        )
        env = 0.65 + 0.35 * np.sin(2.0 * math.pi * 4.0 * t)
        return (carrier * env * 0.38).astype(np.float32)

    def synthesize(
        self,
        chunks: list[SynthChunkInput],
        voice: PreparedVoice,
        params: VoiceParams,
        *,
        request_id: str = "req-mock",
        language: str = "tr",
    ) -> BatchResult:
        def _process_one(chunk: SynthChunkInput) -> SynthChunkOutput:
            samples: np.ndarray | None = None
            if self._use_speech:
                samples = self._synthesize_gtts_single(chunk.text, language)
            if samples is None or samples.size == 0:
                samples = self._synthesize_tone_single(chunk, voice, params)

            dur_ms = round((samples.size / float(self._sample_rate)) * 1000.0)
            return SynthChunkOutput(
                chunk_id=chunk.id,
                samples=samples,
                sample_rate=self._sample_rate,
                duration_ms=dur_ms,
                attempts=1,
                engine_id=self.engine_id,
                from_cache=False,
            )

        if self._use_speech and len(chunks) > 1:
            with ThreadPoolExecutor(max_workers=min(4, len(chunks))) as pool:
                outputs = list(pool.map(_process_one, chunks))
        else:
            outputs = [_process_one(c) for c in chunks]

        return BatchResult(
            engine_id=self.engine_id,
            family=self.family,
            model_revision=self._model_revision,
            gpu_ms=max(1, len(outputs) * 5),
            outputs=outputs,
            failed_ids=[],
        )
