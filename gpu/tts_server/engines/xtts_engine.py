"""Coqui XTTS-v2 (24 kHz) yedek TTS motor sarmalayıcısı."""

from __future__ import annotations

import logging
from typing import Any

import numpy as np

from tts_server.engines.base import BaseTtsServerEngine
from tts_server.schemas import SynthesizeParams
from tts_server.voice_store import VoiceEntry

logger = logging.getLogger(__name__)


class XttsEngine(BaseTtsServerEngine):
    """Coqui XTTS-v2 modelini yükler ve aynı BaseTtsServerEngine arayüzünü uygular."""

    name = "xtts2"

    def __init__(
        self,
        model_id: str = "tts_models/multilingual/multi-dataset/xtts_v2",
        device: str = "cuda",
    ) -> None:
        self.model_id = model_id
        self.device = device
        self.model_revision = "xtts_v2-coqui"
        self.sample_rate = 24000
        self.is_loaded = False
        self._tts: Any = None

    def load(self) -> None:
        from TTS.api import TTS

        logger.info("XTTS-v2 modeli yükleniyor: model_id=%s, device=%s", self.model_id, self.device)
        self._tts = TTS(self.model_id).to(self.device)
        self.is_loaded = True
        logger.info("XTTS-v2 modeli yüklendi: sample_rate=%d", self.sample_rate)

    def generate(
        self,
        text: str,
        voice: VoiceEntry,
        params: SynthesizeParams,
        language: str = "tr",
        seed: int | None = None,
    ) -> np.ndarray:
        if not self.is_loaded or self._tts is None:
            raise RuntimeError("XTTS-v2 modeli henüz yüklenmedi.")

        if seed is not None:
            try:
                import torch

                torch.manual_seed(int(seed))
            except (ImportError, RuntimeError):
                logger.debug("Torch seed ayarlanamadı.")

        wav = self._tts.tts(
            text=text,
            speaker_wav=str(voice.wav_path),
            language=language or "tr",
        )
        arr = np.asarray(wav, dtype=np.float32)
        if arr.ndim == 2:
            arr = arr.mean(axis=1)
        return arr
