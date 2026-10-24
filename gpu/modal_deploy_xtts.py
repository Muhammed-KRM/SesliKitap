"""Modal.com Serverless T4/L4 GPU üzerinde Coqui XTTS-v2 yedek TTS sunucusu."""

from __future__ import annotations

from pathlib import Path

import modal

APP_NAME = "seslikitap-tts-xtts2"
VOLUME_NAME = "seslikitap-tts-cache"
SECRET_NAME = "seslikitap-tts-secret"
GPU_DIR = Path(__file__).resolve().parent

app = modal.App(APP_NAME)
volume = modal.Volume.from_name(VOLUME_NAME, create_if_missing=True)

image = (
    modal.Image.debian_slim(python_version="3.11")
    .apt_install("ffmpeg", "libsndfile1", "git")
    .pip_install_from_requirements(str(GPU_DIR / "requirements-xtts.txt"))
    .env(
        {
            "HF_HOME": "/cache/hf",
            "COQUI_TOS_AGREED": "1",
            "TTS_VOICES_DIR": "/cache/voices",
            "TTS_ENGINE": "xtts2",
            "TTS_MODEL_ID": "tts_models/multilingual/multi-dataset/xtts_v2",
        }
    )
    .add_local_python_source("tts_server")
)


@app.cls(
    gpu="T4",
    image=image,
    volumes={"/cache": volume},
    secrets=[modal.Secret.from_name(SECRET_NAME)],
    scaledown_window=300,
    timeout=600,
)
@modal.concurrent(max_inputs=1)
class XttsServer:
    """Modal T4 konteyneri: başlangıçta XTTS-v2 modelini yükler ve aynı REST sözleşmesini sunar."""

    @modal.enter()
    def load_model(self) -> None:
        from tts_server.api import create_app
        from tts_server.config import TtsServerSettings
        from tts_server.engines.xtts_engine import XttsEngine
        from tts_server.logging_setup import setup_server_logging
        from tts_server.voice_store import VoiceStore

        setup_server_logging()
        self.settings = TtsServerSettings()
        self.voice_store = VoiceStore(
            voices_dir=self.settings.tts_voices_dir,
            max_items=self.settings.tts_max_cached_voices,
        )
        self.engine = XttsEngine(
            model_id=self.settings.tts_model_id,
            device=self.settings.tts_device,
        )
        self.engine.load()
        self.fastapi_app = create_app(
            engine=self.engine,
            voice_store=self.voice_store,
            settings=self.settings,
        )

    @modal.asgi_app()
    def web(self):
        return self.fastapi_app
