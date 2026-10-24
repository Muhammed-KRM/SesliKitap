"""Uvicorn giriş noktası (Kaggle/Colab/RunPod veya yerel Mock sunucusu)."""

from __future__ import annotations

from fastapi import FastAPI

from tts_server.api import create_app
from tts_server.config import TtsServerSettings
from tts_server.engines.base import BaseTtsServerEngine
from tts_server.engines.mock_engine import MockServerEngine
from tts_server.engines.voxcpm_engine import VoxCpmEngine
from tts_server.engines.xtts_engine import XttsEngine
from tts_server.logging_setup import setup_server_logging
from tts_server.voice_store import VoiceStore


def build_engine(settings: TtsServerSettings) -> BaseTtsServerEngine:
    """Ayarlara göre uygun TTS motorunu oluşturur ve yükler."""
    if settings.tts_engine == "mock":
        if not settings.tts_allow_mock:
            raise RuntimeError(
                "Mock TTS motoru yalnızca TTS_ALLOW_MOCK=true olduğunda kullanılabilir."
            )
        engine: BaseTtsServerEngine = MockServerEngine()
    elif settings.tts_engine == "xtts2":
        engine = XttsEngine(model_id=settings.tts_model_id, device=settings.tts_device)
    else:
        engine = VoxCpmEngine(
            model_id=settings.tts_model_id,
            device=settings.tts_device,
            cache_dir=settings.hf_home,
        )
    engine.load()
    return engine


def create_local_app(settings: TtsServerSettings | None = None) -> FastAPI:
    cfg = settings or TtsServerSettings()
    setup_server_logging(cfg.log_level)
    voice_store = VoiceStore(
        voices_dir=cfg.tts_voices_dir,
        max_items=cfg.tts_max_cached_voices,
    )
    engine = build_engine(cfg)
    return create_app(engine=engine, voice_store=voice_store, settings=cfg)


def _create_default_app() -> FastAPI:
    cfg = TtsServerSettings()
    if cfg.tts_engine == "mock" and cfg.tts_allow_mock:
        return create_local_app(cfg)
    return FastAPI(title="SesliKitap TTS Server (Unconfigured)")


app = _create_default_app()
