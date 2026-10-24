"""audio_codec birim testleri (FLAC/WAV kodlama/çözme ve dBFS metrikleri)."""

from __future__ import annotations

import math

import numpy as np
from tts_server import audio_codec


def test_encode_and_decode_flac_and_wav_roundtrip() -> None:
    sr = 24000
    t = np.linspace(0.0, 1.0, sr, endpoint=False, dtype=np.float32)
    sine = (0.5 * np.sin(2.0 * math.pi * 440.0 * t)).astype(np.float32)

    for fmt in ("flac", "wav_pcm16"):
        b64 = audio_codec.encode_audio_b64(sine, sample_rate=sr, output_format=fmt)
        raw = audio_codec.decode_b64_bytes(b64)
        decoded, out_sr = audio_codec.decode_audio_bytes(raw)
        assert out_sr == sr
        assert len(decoded) == sr
        assert np.max(np.abs(decoded - sine)) < 0.001

    duration_ms, peak_dbfs, rms_dbfs = audio_codec.compute_signal_metrics(sine, sr)
    assert duration_ms == 1000
    assert -6.2 <= peak_dbfs <= -5.8
    assert -9.2 <= rms_dbfs <= -8.8
