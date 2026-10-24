"""Birim testleri için sentetik ses dosyası (WAV / FLAC) üretici fabrika fonksiyonları."""

from __future__ import annotations

import base64
import hashlib
import math
from io import BytesIO
from pathlib import Path

import numpy as np
import soundfile as sf


def write_sine_wav(
    path: Path,
    *,
    duration_sec: float = 8.0,
    sample_rate: int = 24000,
    freq_hz: float = 200.0,
    leading_silence_sec: float = 0.0,
    trailing_silence_sec: float = 0.0,
    amplitude: float = 0.4,
) -> Path:
    """Belirtilen süre ve örnekleme hızında sentetik konuşma benzeri WAV dosyası yazar."""
    path.parent.mkdir(parents=True, exist_ok=True)
    lead_n = round(leading_silence_sec * sample_rate)
    trail_n = round(trailing_silence_sec * sample_rate)
    body_n = max(1, round(duration_sec * sample_rate))

    t = np.linspace(0.0, duration_sec, body_n, endpoint=False, dtype=np.float32)
    body = (
        amplitude * np.sin(2.0 * math.pi * freq_hz * t)
        + (amplitude * 0.4) * np.sin(2.0 * math.pi * (freq_hz * 2.0) * t)
    ).astype(np.float32)

    parts = []
    if lead_n > 0:
        parts.append(np.zeros(lead_n, dtype=np.float32))
    parts.append(body)
    if trail_n > 0:
        parts.append(np.zeros(trail_n, dtype=np.float32))

    full = np.concatenate(parts) if len(parts) > 1 else body
    sf.write(str(path), full, sample_rate, format="WAV", subtype="PCM_16")
    return path


def make_flac_b64(duration_sec: float = 1.5, sample_rate: int = 24000) -> tuple[str, str]:
    """(b64_str, sha256_hex) olarak sentetik FLAC üretir."""
    n = round(duration_sec * sample_rate)
    t = np.linspace(0.0, duration_sec, n, endpoint=False, dtype=np.float32)
    wave = (0.35 * np.sin(2.0 * math.pi * 210.0 * t)).astype(np.float32)
    with BytesIO() as bio:
        sf.write(bio, wave, sample_rate, format="FLAC", subtype="PCM_16")
        raw = bio.getvalue()
    return base64.b64encode(raw).decode("ascii"), hashlib.sha256(raw).hexdigest()
