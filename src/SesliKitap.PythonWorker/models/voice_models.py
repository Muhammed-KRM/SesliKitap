"""Ses profili hazırlama ve TTS motor soyutlaması veri modelleri."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import numpy as np


@dataclass(slots=True)
class PreparedReference:
    """ReferencePreparer tarafından temizlenmiş 24 kHz mono WAV referans ses."""

    wav_path: Path
    duration_sec: float
    sha256: str


@dataclass(slots=True)
class PreparedVoice:
    """TTS motorlarına gönderilmeye hazır ses profili bilgisi."""

    voice_id: str
    voice_hash: str
    wav_path: Path
    prompt_text: str | None = None
    duration_sec: float = 0.0


@dataclass(slots=True)
class VoiceParams:
    """TTS sentez parametreleri."""

    cfg_value: float = 2.0
    inference_timesteps: int = 8
    konusma_hizi: float = 1.0
    seed: int | None = None

    def cache_dict(self) -> dict[str, Any]:
        """Önbellek anahtarına dahil edilecek sesi etkileyen parametreler (konusmaHizi hariç)."""
        return {
            "cfgValue": round(float(self.cfg_value), 3),
            "inferenceTimesteps": int(self.inference_timesteps),
            "seed": self.seed,
        }


@dataclass(slots=True)
class EngineInfo:
    """Uzak TTS sunucusu /v1/info yanıtı."""

    engine: str
    model_id: str
    model_revision: str
    sample_rate: int
    max_batch: int = 16
    max_chars_per_chunk: int = 600


@dataclass(slots=True)
class SynthChunkInput:
    """TTS motoruna gönderilecek tek bir metin parçası."""

    id: str
    text: str
    seed: int | None = None


@dataclass(slots=True)
class SynthChunkOutput:
    """TTS motorundan dönen tek bir parçanın ses verisi."""

    chunk_id: str
    samples: np.ndarray
    sample_rate: int
    duration_ms: int
    attempts: int = 1
    engine_id: str = ""
    from_cache: bool = False


@dataclass(slots=True)
class BatchResult:
    """Tek bir parti sentez çağrısının sonucu."""

    engine_id: str
    family: str
    model_revision: str
    gpu_ms: int
    outputs: list[SynthChunkOutput] = field(default_factory=list)
    failed_ids: list[str] = field(default_factory=list)
