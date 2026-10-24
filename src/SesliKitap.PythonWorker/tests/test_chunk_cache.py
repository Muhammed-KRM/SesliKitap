"""ChunkCache birim testleri (Bölüm 4.15.8)."""

from __future__ import annotations

import numpy as np
import pytest

from models.synthesis_models import ChunkAudio
from models.voice_models import VoiceParams
from pipeline.tts.chunk_cache import ChunkCache
from tests.conftest import FakeMinioClient, FakeRedis


def test_chunk_cache_key_is_deterministic_and_ignores_speaking_rate() -> None:
    p1 = VoiceParams(cfg_value=2.0, inference_timesteps=10, konusma_hizi=1.0, seed=42)
    p2 = VoiceParams(cfg_value=2.0, inference_timesteps=10, konusma_hizi=1.5, seed=42)
    p3 = VoiceParams(cfg_value=2.5, inference_timesteps=10, konusma_hizi=1.0, seed=42)

    k1 = ChunkCache.key("voxcpm2", "rev1", "hash123", p1, "Merhaba dünya.")
    k2 = ChunkCache.key("voxcpm2", "rev1", "hash123", p2, "Merhaba dünya.")
    k3 = ChunkCache.key("voxcpm2", "rev1", "hash123", p3, "Merhaba dünya.")

    assert k1 == k2
    assert k1 != k3


def test_chunk_cache_put_and_get_roundtrip(fake_minio: FakeMinioClient, fake_redis: FakeRedis) -> None:
    cache = ChunkCache(minio=fake_minio, redis_client=fake_redis)  # type: ignore[arg-type]
    params = VoiceParams(cfg_value=2.0, inference_timesteps=10, konusma_hizi=1.0)
    key = ChunkCache.key("voxcpm2", "rev1", "hash123", params, "Deneme cümlesi.")

    t = np.linspace(0, 1.0, 24000, endpoint=False, dtype=np.float32)
    samples = 0.25 * np.sin(2.0 * np.pi * 440.0 * t).astype(np.float32)
    original = ChunkAudio(chunk_id="c-1", samples=samples, sample_rate=24000, attempts=1, engine_id="voxcpm2_modal")

    cache.put(key, original)
    loaded = cache.get(key, chunk_id="c-1")

    assert loaded is not None
    assert loaded.from_cache is True
    assert loaded.chunk_id == "c-1"
    assert loaded.sample_rate == 24000
    assert len(loaded.samples) == pytest.approx(24000, abs=10)
