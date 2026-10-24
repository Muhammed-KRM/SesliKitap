"""FastAPI uygulama fabrikası ve Bölüm 4.8 HTTP TTS uç noktaları."""

from __future__ import annotations

import json
import logging
import threading
import time

from fastapi import Depends, FastAPI, HTTPException, Request, Response, status
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

from tts_server import __version__, audio_codec
from tts_server.auth import require_bearer_token
from tts_server.config import TtsServerSettings
from tts_server.engines.base import BaseTtsServerEngine
from tts_server.gpu_timer import measure_gpu_ms, query_gpu_status
from tts_server.schemas import (
    HealthResponse,
    InfoResponse,
    SynthesizeRequest,
    SynthesizeResponse,
    SynthesizeResultItem,
    VoiceRegisterRequest,
    VoiceRegisterResponse,
)
from tts_server.voice_store import (
    InvalidVoiceAudioError,
    VoiceHashMismatchError,
    VoiceStore,
)

logger = logging.getLogger(__name__)


def _error_response(
    status_code: int,
    code: str,
    message: str,
    retryable: bool = False,
) -> JSONResponse:
    return JSONResponse(
        status_code=status_code,
        content={
            "error": {
                "code": code,
                "message": message,
                "retryable": retryable,
            }
        },
    )


def create_app(
    engine: BaseTtsServerEngine,
    voice_store: VoiceStore,
    settings: TtsServerSettings | None = None,
) -> FastAPI:
    """Verilen motor ve ses deposu ile FastAPI uygulamasını oluşturur."""
    cfg = settings or TtsServerSettings()
    started_at = time.monotonic()
    gpu_lock = threading.Lock()

    app = FastAPI(
        title="SesliKitap GPU TTS Server",
        version=__version__,
        docs_url="/docs",
        redoc_url=None,
    )
    app.state.settings = cfg
    app.state.engine = engine
    app.state.voice_store = voice_store
    app.state.started_at = started_at

    @app.exception_handler(HTTPException)
    async def http_exception_handler(_request: Request, exc: HTTPException) -> JSONResponse:
        if isinstance(exc.detail, dict) and "code" in exc.detail:
            return _error_response(
                status_code=exc.status_code,
                code=str(exc.detail.get("code", "http_error")),
                message=str(exc.detail.get("message", "İstek işlenemedi.")),
                retryable=bool(exc.detail.get("retryable", False)),
            )
        return _error_response(
            status_code=exc.status_code,
            code="http_error",
            message=str(exc.detail),
            retryable=False,
        )

    @app.exception_handler(RequestValidationError)
    async def validation_exception_handler(
        _request: Request, exc: RequestValidationError
    ) -> JSONResponse:
        return _error_response(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            code="validation_error",
            message=f"İstek doğrulama hatası: {exc.errors()}",
            retryable=False,
        )

    @app.get("/health", response_model=HealthResponse)
    @app.get("/v1/health", response_model=HealthResponse)
    def health(response: Response) -> HealthResponse:
        gpu_name, vram_free_mb = query_gpu_status()
        uptime_sec = int(max(0.0, time.monotonic() - started_at))
        loaded = bool(engine.is_loaded)
        if not loaded:
            response.status_code = status.HTTP_503_SERVICE_UNAVAILABLE
        return HealthResponse(
            status="ok" if loaded else "starting",
            service="seslikitap-tts",
            version=__version__,
            engine=engine.name,
            model_loaded=loaded,
            gpu=gpu_name,
            vram_free_mb=vram_free_mb,
            uptime_sec=uptime_sec,
        )

    @app.get(
        "/v1/info",
        response_model=InfoResponse,
        dependencies=[Depends(require_bearer_token)],
    )
    def info() -> InfoResponse:
        gpu_name, _ = query_gpu_status()
        return InfoResponse(
            engine=engine.name,
            model_id=engine.model_id,
            model_revision=engine.model_revision,
            lora_path=None,
            sample_rate=engine.sample_rate,
            device=cfg.tts_device,
            gpu_name=gpu_name,
            max_batch=cfg.tts_max_batch_items,
            max_batch_items=cfg.tts_max_batch_items,
            max_chars_per_chunk=cfg.tts_max_text_chars,
            max_text_chars=cfg.tts_max_text_chars,
            supported_languages=["tr", "en"],
            supports_streaming=False,
            cached_voices=voice_store.count(),
        )

    @app.head(
        "/v1/voices/{voice_hash}",
        dependencies=[Depends(require_bearer_token)],
    )
    def check_voice(voice_hash: str) -> Response:
        try:
            exists = voice_store.exists(voice_hash)
        except VoiceHashMismatchError as exc:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail={
                    "code": "voice_hash_mismatch",
                    "message": str(exc),
                    "retryable": False,
                },
            ) from exc
        if not exists:
            return Response(status_code=status.HTTP_404_NOT_FOUND)
        return Response(status_code=status.HTTP_200_OK)

    @app.put(
        "/v1/voices/{voice_hash}",
        response_model=VoiceRegisterResponse,
        status_code=status.HTTP_201_CREATED,
        dependencies=[Depends(require_bearer_token)],
    )
    async def register_voice(
        voice_hash: str,
        request: Request,
    ) -> VoiceRegisterResponse:
        content_type = (request.headers.get("content-type") or "").lower()
        raw_bytes: bytes
        prompt_text: str | None = None

        if "multipart/form-data" in content_type:
            form = await request.form()
            audio_field = form.get("audio")
            if audio_field is None or not hasattr(audio_field, "read"):
                raise HTTPException(
                    status_code=status.HTTP_400_BAD_REQUEST,
                    detail={
                        "code": "missing_audio_file",
                        "message": "Multipart istekte 'audio' alanı zorunludur.",
                        "retryable": False,
                    },
                )
            raw_bytes = await audio_field.read()  # type: ignore[misc]
            raw_prompt = form.get("promptText") or form.get("prompt_text")
            prompt_text = str(raw_prompt) if raw_prompt is not None else None
        else:
            try:
                body_data = json.loads((await request.body()).decode("utf-8"))
                payload = VoiceRegisterRequest.model_validate(body_data)
                raw_bytes = audio_codec.decode_b64_bytes(payload.audio_b64)
                prompt_text = payload.prompt_text
            except Exception as exc:
                raise HTTPException(
                    status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                    detail={
                        "code": "invalid_voice_audio",
                        "message": f"Referans ses verisi çözümlenemedi: {exc}",
                        "retryable": False,
                    },
                ) from exc

        try:
            entry = voice_store.put(
                voice_hash=voice_hash,
                raw_audio_bytes=raw_bytes,
                prompt_text=prompt_text,
            )
        except VoiceHashMismatchError as exc:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail={
                    "code": "voice_hash_mismatch",
                    "message": str(exc),
                    "retryable": False,
                },
            ) from exc
        except InvalidVoiceAudioError as exc:
            raise HTTPException(
                status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                detail={
                    "code": "invalid_voice_audio",
                    "message": str(exc),
                    "retryable": False,
                },
            ) from exc

        logger.info(
            "Referans ses kaydedildi: voice_hash=%s, duration_sec=%.2f, sr=%d",
            entry.voice_hash[:12],
            entry.duration_sec,
            entry.sample_rate,
        )
        return VoiceRegisterResponse(
            voice_hash=entry.voice_hash,
            cached=True,
            duration_sec=entry.duration_sec,
        )

    @app.post(
        "/v1/synthesize",
        response_model=SynthesizeResponse,
        dependencies=[Depends(require_bearer_token)],
    )
    def synthesize(req: SynthesizeRequest) -> SynthesizeResponse:
        if not engine.is_loaded:
            raise HTTPException(
                status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
                detail={
                    "code": "model_not_ready",
                    "message": "TTS modeli henüz hazır değil.",
                    "retryable": True,
                },
            )

        if req.engine and req.engine != engine.name:
            raise HTTPException(
                status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                detail={
                    "code": "engine_mismatch",
                    "message": f"İstenen motor '{req.engine}', sunucudaki motor '{engine.name}' ile uyuşmuyor.",
                    "retryable": False,
                },
            )

        chunks = req.chunks or req.items or []
        if len(chunks) > cfg.tts_max_batch_items:
            raise HTTPException(
                status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                detail={
                    "code": "batch_too_large",
                    "message": f"Parti başına en fazla {cfg.tts_max_batch_items} öğe gönderilebilir.",
                    "retryable": False,
                },
            )

        for item in chunks:
            if len(item.text) > cfg.tts_max_text_chars:
                raise HTTPException(
                    status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                    detail={
                        "code": "text_too_long",
                        "message": f"'{item.id}' öğesi izin verilen {cfg.tts_max_text_chars} karakter sınırını aşıyor.",
                        "retryable": False,
                    },
                )

        voice = voice_store.get(req.voice_hash)
        if voice is None:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail={
                    "code": "voice_not_cached",
                    "message": "Referans ses bu konteynerde önbellekte bulunamadı; PUT /v1/voices/{hash} çağırın.",
                    "retryable": True,
                },
            )

        results: list[SynthesizeResultItem] = []
        try:
            with gpu_lock, measure_gpu_ms() as gpu_watch:
                for item in chunks:
                    seed = item.seed if item.seed is not None else req.params.seed
                    samples = engine.generate(
                        text=item.text,
                        voice=voice,
                        params=req.params,
                        language=req.language,
                        seed=seed,
                    )
                    out_sr = engine.sample_rate
                    duration_ms, peak_dbfs, rms_dbfs = audio_codec.compute_signal_metrics(
                        samples, out_sr
                    )
                    audio_b64 = audio_codec.encode_audio_b64(
                        samples,
                        sample_rate=out_sr,
                        output_format=req.output_format,
                    )
                    results.append(
                        SynthesizeResultItem(
                            id=item.id,
                            ok=True,
                            audio_b64=audio_b64,
                            duration_ms=duration_ms,
                            attempts=1,
                            sample_rate=out_sr,
                            peak_dbfs=peak_dbfs,
                            rms_dbfs=rms_dbfs,
                        )
                    )
        except HTTPException:
            raise
        except RuntimeError as exc:
            msg = str(exc)
            is_oom = "out of memory" in msg.lower()
            if is_oom:
                try:
                    import torch

                    if torch.cuda.is_available():
                        torch.cuda.empty_cache()
                except (ImportError, RuntimeError):
                    logger.debug("CUDA önbellek temizliği atlandı.")
            raise HTTPException(
                status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
                detail={
                    "code": "cuda_oom" if is_oom else "engine_runtime_error",
                    "message": "GPU bellek sınırı aşıldı." if is_oom else msg,
                    "retryable": True,
                },
            ) from exc

        logger.info(
            "Sentez tamamlandı: request_id=%s, engine=%s, items=%d, gpu_ms=%d",
            req.request_id,
            engine.name,
            len(results),
            gpu_watch.elapsed_ms,
        )
        return SynthesizeResponse(
            request_id=req.request_id,
            engine=engine.name,
            model_revision=engine.model_revision,
            sample_rate=engine.sample_rate,
            gpu_ms=gpu_watch.elapsed_ms,
            results=results,
        )

    return app
