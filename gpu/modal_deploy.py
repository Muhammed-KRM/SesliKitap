"""Modal.com Serverless L4 GPU üzerinde OpenBMB VoxCPM2 TTS sunucusu.

Aynı kod iki farklı modelle (A/B kulak testi için) dağıtılabilir; model ve uygulama adı
dağıtım sırasındaki ortam değişkenlerinden okunur:

    # Temel model (Apache-2.0, Türkçe resmi destekli)
    modal deploy gpu/modal_deploy.py

    # Türkçe LoRA birleştirilmiş sürüm (Trendyol, MIT)
    $env:SK_TTS_MODEL_ID = "Trendyol/Trendyol-TTS"; $env:SK_TTS_APP_SUFFIX = "trendyol"
    modal deploy gpu/modal_deploy.py
"""

from __future__ import annotations

import os
from pathlib import Path

import modal

MODEL_ID = os.environ.get("SK_TTS_MODEL_ID", "openbmb/VoxCPM2")
APP_SUFFIX = os.environ.get("SK_TTS_APP_SUFFIX", "").strip()
APP_NAME = "seslikitap-tts-voxcpm2" + (f"-{APP_SUFFIX}" if APP_SUFFIX else "")
VOLUME_NAME = "seslikitap-tts-cache"
SECRET_NAME = "seslikitap-tts-secret"
GPU_DIR = Path(__file__).resolve().parent

app = modal.App(APP_NAME)
volume = modal.Volume.from_name(VOLUME_NAME, create_if_missing=True)

image = (
    modal.Image.debian_slim(python_version="3.11")
    .apt_install("ffmpeg", "libsndfile1", "git")
    .pip_install_from_requirements(str(GPU_DIR / "requirements.txt"))
    .env(
        {
            "HF_HOME": "/cache/hf",
            "TTS_VOICES_DIR": "/cache/voices",
            "TTS_ENGINE": "voxcpm2",
            "TTS_MODEL_ID": MODEL_ID,
        }
    )
    .add_local_python_source("tts_server")
)


@app.cls(
    gpu="L4",
    image=image,
    volumes={"/cache": volume},
    secrets=[modal.Secret.from_name(SECRET_NAME)],
    scaledown_window=120,
    timeout=600,
)
@modal.concurrent(max_inputs=1)
class VoxCpmServer:
    """Modal L4 konteyneri: başlangıçta VoxCPM2'yi GPU belleğine yükler ve FastAPI sunar."""

    @modal.enter()
    def load_model(self) -> None:
        from tts_server.api import create_app
        from tts_server.config import TtsServerSettings
        from tts_server.engines.voxcpm_engine import VoxCpmEngine
        from tts_server.logging_setup import setup_server_logging
        from tts_server.voice_store import VoiceStore

        setup_server_logging()
        self.settings = TtsServerSettings()
        self.voice_store = VoiceStore(
            voices_dir=self.settings.tts_voices_dir,
            max_items=self.settings.tts_max_cached_voices,
        )
        self.engine = VoxCpmEngine(
            model_id=self.settings.tts_model_id,
            device=self.settings.tts_device,
            cache_dir=self.settings.hf_home,
        )
        self.engine.load()
        # İndirilen model ağırlıkları ve kayıtlı referans sesler sonraki soğuk başlatmalar için kalıcı olsun.
        volume.commit()
        self.fastapi_app = create_app(
            engine=self.engine,
            voice_store=self.voice_store,
            settings=self.settings,
        )

    @modal.asgi_app()
    def web(self):
        return self.fastapi_app
