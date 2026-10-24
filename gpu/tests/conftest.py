"""gpu/tests için ortak pytest fikstürleri (GPU gerektirmez, MockServerEngine kullanır)."""

from __future__ import annotations

import base64
import hashlib
import math
import sys
from io import BytesIO
from pathlib import Path

GPU_ROOT = Path(__file__).resolve().parents[1]
if str(GPU_ROOT) not in sys.path:
    sys.path.insert(0, str(GPU_ROOT))

import numpy as np
import pytest
import soundfile as sf
from fastapi.testclient import TestClient
from tts_server.api import create_app  # noqa: E402
from tts_server.config import TtsServerSettings  # noqa: E402
from tts_server.engines.mock_engine import MockServerEngine  # noqa: E402
from tts_server.voice_store import VoiceStore  # noqa: E402

TEST_TOKEN = "test-bearer-token-32-characters-long"


def make_test_wav_bytes(duration_sec: float = 6.0, sample_rate: int = 24000) -> tuple[bytes, str, str]:
    """(raw_bytes, b64_str, sha256_hex) olarak sentetik referans WAV üretir."""
    total = round(duration_sec * sample_rate)
    t = np.linspace(0.0, duration_sec, total, endpoint=False, dtype=np.float32)
    samples = (0.35 * np.sin(2.0 * math.pi * 190.0 * t)).astype(np.float32)
    with BytesIO() as bio:
        sf.write(bio, samples, sample_rate, format="WAV", subtype="PCM_16")
        raw = bio.getvalue()
    b64 = base64.b64encode(raw).decode("ascii")
    sha = hashlib.sha256(raw).hexdigest().lower()
    return raw, b64, sha


@pytest.fixture()
def settings(tmp_path: Path) -> TtsServerSettings:
    return TtsServerSettings(
        TTS_AUTH_TOKEN=TEST_TOKEN,
        TTS_ENGINE="mock",
        TTS_ALLOW_MOCK=True,
        TTS_VOICES_DIR=tmp_path / "voices",
        HF_HOME=tmp_path / "hf",
        TTS_MAX_BATCH_ITEMS=4,
        TTS_MAX_TEXT_CHARS=120,
        TTS_MAX_CACHED_VOICES=3,
    )


@pytest.fixture()
def voice_store(settings: TtsServerSettings) -> VoiceStore:
    return VoiceStore(
        voices_dir=settings.tts_voices_dir,
        max_items=settings.tts_max_cached_voices,
    )


@pytest.fixture()
def mock_engine() -> MockServerEngine:
    engine = MockServerEngine(sample_rate=24000)
    engine.load()
    return engine


@pytest.fixture()
def client(
    mock_engine: MockServerEngine,
    voice_store: VoiceStore,
    settings: TtsServerSettings,
) -> TestClient:
    app = create_app(engine=mock_engine, voice_store=voice_store, settings=settings)
    return TestClient(app)


@pytest.fixture()
def auth_headers() -> dict[str, str]:
    return {"Authorization": f"Bearer {TEST_TOKEN}"}
