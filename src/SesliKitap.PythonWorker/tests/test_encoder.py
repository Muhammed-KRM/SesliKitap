"""Encoder (Opus ve MP3 kodlama) birim testleri."""

from __future__ import annotations

from pathlib import Path

from pipeline.audio.encoder import Encoder
from tests.factories.audio_factory import write_sine_wav


def test_encoder_produces_valid_opus_and_mp3_files(tmp_path: Path) -> None:
    wav = write_sine_wav(tmp_path / "input.wav", duration_sec=3.0, sample_rate=24000)
    encoder = Encoder()

    opus_out = tmp_path / "out.ogg"
    dur_opus = encoder.to_opus(
        wav,
        opus_out,
        bitrate_kbps=48,
        tags={"title": "Bölüm 1", "artist": "Yazar", "album": "Kitap"},
    )
    assert opus_out.is_file()
    assert opus_out.stat().st_size > 500
    assert 2.5 <= dur_opus <= 3.5

    mp3_out = tmp_path / "out.mp3"
    dur_mp3 = encoder.to_mp3(
        wav,
        mp3_out,
        bitrate_kbps=64,
        tags={"title": "Bölüm 1", "artist": "Yazar", "album": "Kitap", "track": "1/10"},
    )
    assert mp3_out.is_file()
    assert mp3_out.stat().st_size > 500
    assert 2.5 <= dur_mp3 <= 3.5
