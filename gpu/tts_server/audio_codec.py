"""Bellek içi FLAC/WAV kodlama, çözme ve tepe/RMS dBFS sinyal ölçümü."""

from __future__ import annotations

import base64
import math
from io import BytesIO
from typing import Literal

import numpy as np
import soundfile as sf


def decode_b64_bytes(b64_text: str) -> bytes:
    """Base64 metnini ham baytlara çözer."""
    return base64.b64decode(b64_text.encode("ascii"), validate=True)


def decode_audio_bytes(raw_bytes: bytes) -> tuple[np.ndarray, int]:
    """WAV/FLAC baytlarını mono float32 numpy dizisine ve örnekleme hızına çözer."""
    with BytesIO(raw_bytes) as bio:
        samples, sample_rate = sf.read(bio, dtype="float32", always_2d=False)
    arr = np.asarray(samples, dtype=np.float32)
    if arr.ndim == 2:
        arr = arr.mean(axis=1)
    return arr, int(sample_rate)


def encode_audio_b64(
    samples: np.ndarray,
    sample_rate: int,
    output_format: Literal["flac", "wav_pcm16"] = "flac",
) -> str:
    """Mono float32 ses dizisini FLAC veya WAV PCM16 olarak kodlayıp Base64 döndürür."""
    arr = np.asarray(samples, dtype=np.float32)
    if arr.ndim == 2:
        arr = arr.mean(axis=1)
    arr = np.clip(arr, -1.0, 1.0)

    fmt = "FLAC" if output_format == "flac" else "WAV"
    with BytesIO() as bio:
        sf.write(bio, arr, sample_rate, format=fmt, subtype="PCM_16")
        encoded = base64.b64encode(bio.getvalue()).decode("ascii")
    return encoded


def compute_signal_metrics(
    samples: np.ndarray,
    sample_rate: int,
) -> tuple[int, float, float]:
    """(duration_ms, peak_dbfs, rms_dbfs) hesaplar."""
    arr = np.asarray(samples, dtype=np.float32)
    if arr.ndim == 2:
        arr = arr.mean(axis=1)

    if arr.size == 0 or sample_rate <= 0:
        return 0, -120.0, -120.0

    duration_ms = round((arr.size / float(sample_rate)) * 1000.0)
    peak = float(np.max(np.abs(arr)))
    rms = float(np.sqrt(np.mean(np.square(arr, dtype=np.float64))))

    peak_dbfs = 20.0 * math.log10(max(peak, 1e-6))
    rms_dbfs = 20.0 * math.log10(max(rms, 1e-6))
    return duration_ms, round(peak_dbfs, 2), round(rms_dbfs, 2)
