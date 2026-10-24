"""Deterministik sahte TTS motoru (GPU olmadan yerel geliştirme, birim test ve CI/CD için)."""

from __future__ import annotations

import hashlib
import math

import numpy as np

from tts_server.engines.base import BaseTtsServerEngine
from tts_server.schemas import SynthesizeParams
from tts_server.voice_store import VoiceEntry


class MockServerEngine(BaseTtsServerEngine):
    """Metin uzunluğuyla orantılı (karakter × 65 ms) doğal zarflı harmonik sinyal üretir."""

    name = "mock"

    def __init__(self, sample_rate: int = 24000) -> None:
        self.model_id = "seslikitap/mock-tts-v1"
        self.model_revision = "mock-1.0.0"
        self.sample_rate = sample_rate
        self.is_loaded = False

    def load(self) -> None:
        self.is_loaded = True

    def generate(
        self,
        text: str,
        voice: VoiceEntry,
        params: SynthesizeParams,
        language: str = "tr",
        seed: int | None = None,
    ) -> np.ndarray:
        if not self.is_loaded:
            self.load()

        clean_len = max(8, len(text.strip()))
        # Türkçe ortalama konuşma hızı: karakter başına ~65 ms (QualityChecker [25, 160] ms aralığında)
        duration_sec = max(0.65, min(25.0, clean_len * 0.065))
        total_samples = round(duration_sec * self.sample_rate)

        digest = hashlib.sha256(f"{voice.voice_hash}:{text}:{seed or 0}".encode()).digest()
        base_freq = 155.0 + (digest[0] % 90)  # 155–245 Hz temel konuşma frekansı

        t = np.linspace(0.0, duration_sec, total_samples, endpoint=False, dtype=np.float32)
        # Harmonik konuşma benzeri dalga + hece zarfı (QualityChecker mostly_silent ve clipping kurallarına tam uyumlu)
        carrier = (
            0.55 * np.sin(2.0 * math.pi * base_freq * t)
            + 0.25 * np.sin(2.0 * math.pi * (base_freq * 2.0) * t)
            + 0.12 * np.sin(2.0 * math.pi * (base_freq * 3.0) * t)
        )
        syllable_env = 0.65 + 0.35 * np.sin(2.0 * math.pi * 4.2 * t)
        signal = (carrier * syllable_env * 0.38).astype(np.float32)

        # Yumuşak başlangıç/bitiş (5 ms fade)
        fade_len = min(int(0.005 * self.sample_rate), total_samples // 4)
        if fade_len > 1:
            fade = np.linspace(0.0, 1.0, fade_len, dtype=np.float32)
            signal[:fade_len] *= fade
            signal[-fade_len:] *= fade[::-1]

        return signal
