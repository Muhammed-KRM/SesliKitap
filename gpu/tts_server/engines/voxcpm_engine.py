"""OpenBMB VoxCPM2 (2B, 48 kHz) TTS motor sarmalayıcısı (ADR-002 ile doğrulanmıştır)."""

from __future__ import annotations

import inspect
import logging
from pathlib import Path
from typing import Any

import numpy as np

from tts_server.engines.base import BaseTtsServerEngine
from tts_server.schemas import SynthesizeParams
from tts_server.voice_store import VoiceEntry

logger = logging.getLogger(__name__)


class VoxCpmEngine(BaseTtsServerEngine):
    """VoxCPM2 modelini yükler ve sıfır-atış veya Hi-Fi ses klonlama ile sentez yapar."""

    name = "voxcpm2"

    def __init__(
        self,
        model_id: str = "openbmb/VoxCPM2",
        device: str = "cuda",
        cache_dir: Path | None = None,
    ) -> None:
        self.model_id = model_id
        self.device = device
        self.cache_dir = cache_dir
        self.model_revision = "voxcpm2-openbmb"
        self.sample_rate = 48000
        self.is_loaded = False
        self._model: Any = None
        self._supported_params: set[str] = set()

    def load(self) -> None:
        """VoxCPM2 modelini GPU'ya yükler ve imza parametrelerini inceler."""
        from voxcpm import VoxCPM

        logger.info("VoxCPM2 modeli yükleniyor: model_id=%s, device=%s", self.model_id, self.device)
        load_kwargs: dict[str, Any] = {"load_denoiser": False}
        from_pretrained_sig = inspect.signature(VoxCPM.from_pretrained)
        if "device" in from_pretrained_sig.parameters:
            load_kwargs["device"] = self.device
        if self.cache_dir is not None and "cache_dir" in from_pretrained_sig.parameters:
            load_kwargs["cache_dir"] = str(self.cache_dir)

        self._model = VoxCPM.from_pretrained(self.model_id, **load_kwargs)
        tts_inner = getattr(self._model, "tts_model", self._model)
        self.sample_rate = int(getattr(tts_inner, "sample_rate", 48000))
        self._supported_params = set(inspect.signature(self._model.generate).parameters.keys())
        if not ({"reference_wav_path", "prompt_wav_path"} & self._supported_params):
            # Eski voxcpm (<2.0) sürümleri referans sesi hata vermeden yok sayıp klonsuz üretir.
            raise RuntimeError(
                "Yüklü voxcpm sürümü ses klonlamayı desteklemiyor "
                "(generate() içinde reference_wav_path/prompt_wav_path yok). voxcpm>=2.0 gerekli."
            )
        self.is_loaded = True
        logger.info(
            "VoxCPM2 modeli yüklendi: sample_rate=%d, params=%s",
            self.sample_rate,
            sorted(self._supported_params),
        )

    def generate(
        self,
        text: str,
        voice: VoiceEntry,
        params: SynthesizeParams,
        language: str = "tr",
        seed: int | None = None,
    ) -> np.ndarray:
        if not self.is_loaded or self._model is None:
            raise RuntimeError("VoxCPM2 modeli henüz yüklenmedi.")

        if seed is not None:
            try:
                import torch

                torch.manual_seed(int(seed))
                if torch.cuda.is_available():
                    torch.cuda.manual_seed_all(int(seed))
            except (ImportError, RuntimeError):
                logger.debug("Torch seed ayarlanamadı.")

        kwargs: dict[str, Any] = {"text": text}
        if "cfg_value" in self._supported_params:
            kwargs["cfg_value"] = float(params.cfg_value)
        if "inference_timesteps" in self._supported_params:
            kwargs["inference_timesteps"] = int(params.inference_timesteps)
        if "normalize" in self._supported_params:
            kwargs["normalize"] = bool(params.normalize_text)
        if "denoise" in self._supported_params:
            kwargs["denoise"] = bool(params.denoise_prompt)

        wav_str = str(voice.wav_path)
        has_prompt_text = bool(voice.prompt_text and voice.prompt_text.strip())

        if has_prompt_text:
            if "prompt_wav_path" in self._supported_params:
                kwargs["prompt_wav_path"] = wav_str
            if "prompt_text" in self._supported_params:
                kwargs["prompt_text"] = voice.prompt_text
            if "reference_wav_path" in self._supported_params:
                kwargs["reference_wav_path"] = wav_str
        else:
            if "reference_wav_path" in self._supported_params:
                kwargs["reference_wav_path"] = wav_str
            elif "prompt_wav_path" in self._supported_params:
                kwargs["prompt_wav_path"] = wav_str

        wav = self._model.generate(**kwargs)
        arr = np.asarray(wav, dtype=np.float32)
        if arr.ndim == 2:
            arr = arr.mean(axis=1)
        return arr
