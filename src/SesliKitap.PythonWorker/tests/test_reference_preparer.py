"""ReferencePreparer birim testleri (5–30 sn kuralı, 24 kHz dönüşüm, sessizlik kırpma ve SHA-256)."""

from __future__ import annotations

from pathlib import Path

import pytest
import soundfile as sf

from models.events import ProcessingError
from pipeline.audio.reference_preparer import ReferencePreparer
from tests.factories.audio_factory import write_sine_wav


def test_prepare_valid_audio_produces_24k_mono_and_sha256(tmp_path: Path) -> None:
    raw = write_sine_wav(
        tmp_path / "raw_48k.wav",
        duration_sec=8.0,
        sample_rate=48000,
        leading_silence_sec=1.0,
        trailing_silence_sec=1.0,
    )
    preparer = ReferencePreparer()
    result = preparer.prepare(raw, out_wav_path=tmp_path / "reference.wav")

    assert result.wav_path.is_file()
    assert len(result.sha256) == 64
    assert 5.0 <= result.duration_sec <= 10.5

    info = sf.info(str(result.wav_path))
    assert info.samplerate == 24000
    assert info.channels == 1


def test_prepare_rejects_too_short_audio(tmp_path: Path) -> None:
    short_raw = write_sine_wav(tmp_path / "short.wav", duration_sec=2.5, sample_rate=24000)
    preparer = ReferencePreparer()
    with pytest.raises(ProcessingError) as exc_info:
        preparer.prepare(short_raw)
    assert exc_info.value.code == "VOICE_TOO_SHORT"
    assert "En az 5 saniyelik" in exc_info.value.message


def test_prepare_trims_audio_longer_than_30_seconds(tmp_path: Path) -> None:
    long_raw = write_sine_wav(tmp_path / "long.wav", duration_sec=36.0, sample_rate=24000)
    preparer = ReferencePreparer()
    result = preparer.prepare(long_raw, out_wav_path=tmp_path / "ref_trimmed.wav")
    assert 24.5 <= result.duration_sec <= 30.0


def test_prepare_rejects_corrupt_audio(tmp_path: Path) -> None:
    corrupt = tmp_path / "corrupt.wav"
    corrupt.write_bytes(b"NOT_A_VALID_AUDIO_FILE_CONTENT")
    preparer = ReferencePreparer()
    with pytest.raises(ProcessingError) as exc_info:
        preparer.prepare(corrupt)
    assert exc_info.value.code == "VOICE_INVALID_FORMAT"
