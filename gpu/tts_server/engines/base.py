"""Uzak TTS sunucusu motor soyutlaması (SOLID — Open/Closed & Liskov)."""

from __future__ import annotations

from abc import ABC, abstractmethod

import numpy as np

from tts_server.schemas import SynthesizeParams
from tts_server.voice_store import VoiceEntry


class BaseTtsServerEngine(ABC):
    """Sunucu tarafı TTS motorlarının ortak sözleşmesi."""

    name: str
    model_id: str
    model_revision: str
    sample_rate: int
    is_loaded: bool = False

    @abstractmethod
    def load(self) -> None:
        """Model ağırlıklarını belleğe yükler ve ısınma (warm-up) sentezini çalıştırır."""

    @abstractmethod
    def generate(
        self,
        text: str,
        voice: VoiceEntry,
        params: SynthesizeParams,
        language: str = "tr",
        seed: int | None = None,
    ) -> np.ndarray:
        """Verilen metni ve referans sesi kullanarak mono float32 ses dizisi üretir."""
