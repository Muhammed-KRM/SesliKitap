"""Yerel sahte TTS motoru (GPU sunucusu olmadan worker uçtan uca testleri için — Bölüm 4.15.6)."""

from __future__ import annotations

import hashlib
import math

import numpy as np

from models.voice_models import (
    BatchResult,
    EngineInfo,
    PreparedVoice,
    SynthChunkInput,
    SynthChunkOutput,
    VoiceParams,
)
from pipeline.tts.base_engine import BaseTtsEngine


class MockTtsEngine(BaseTtsEngine):
    """Yalnızca allow_mock=True olduğunda etkinleşen deterministik test TTS motoru."""

    engine_id = "mock"
    family = "mock"

    def __init__(self, *, allow_mock: bool = False, sample_rate: int = 24000) -> None:
        self._allow_mock = allow_mock
        self._sample_rate = sample_rate
        self._registered_voices: set[str] = set()

    def is_available(self) -> bool:
        return self._allow_mock

    def info(self) -> EngineInfo:
        return EngineInfo(
            engine="mock",
            model_id="seslikitap/mock-worker-v1",
            model_revision="mock-1.0.0",
            sample_rate=self._sample_rate,
            max_batch=32,
            max_chars_per_chunk=600,
        )

    def has_voice(self, voice_hash: str) -> bool:
        return voice_hash.lower() in self._registered_voices

    def register_voice(self, voice: PreparedVoice) -> None:
        self._registered_voices.add(voice.voice_hash.lower())

    def synthesize(
        self,
        chunks: list[SynthChunkInput],
        voice: PreparedVoice,
        params: VoiceParams,
        *,
        request_id: str = "req-mock",
        language: str = "tr",
    ) -> BatchResult:
        outputs: list[SynthChunkOutput] = []
        for chunk in chunks:
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
            samples = (carrier * env * 0.38).astype(np.float32)

            outputs.append(
                SynthChunkOutput(
                    chunk_id=chunk.id,
                    samples=samples,
                    sample_rate=self._sample_rate,
                    duration_ms=round(duration_sec * 1000.0),
                    attempts=1,
                    engine_id=self.engine_id,
                    from_cache=False,
                )
            )

        return BatchResult(
            engine_id=self.engine_id,
            family=self.family,
            model_revision="mock-1.0.0",
            gpu_ms=max(1, len(outputs) * 5),
            outputs=outputs,
            failed_ids=[],
        )
