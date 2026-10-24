"""VoiceProfileConsumer uçtan uca birim testleri (başarılı hazırlama ve kısa ses reddi)."""

from __future__ import annotations

from pathlib import Path
from unittest.mock import MagicMock

from consumers.voice_profile_consumer import VoiceProfileConsumer
from models.events import VoiceProfileUploadedEvent
from pipeline.tts.engine_chain import TtsEngineChain
from pipeline.tts.mock_engine import MockTtsEngine
from tests.factories.audio_factory import write_sine_wav


def _make_mock_chain() -> TtsEngineChain:
    mock_eng = MockTtsEngine(allow_mock=True)
    return TtsEngineChain(engines={"mock": mock_eng}, order=["mock"])


def test_voice_profile_consumer_success_flow(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.setattr("config.settings.temp_dir", tmp_path / "temp")

    source_wav = write_sine_wav(tmp_path / "source_raw.wav", duration_sec=7.5, sample_rate=24000)

    minio_mock = MagicMock()

    def fake_download(_key: str, dest: Path) -> None:
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(source_wav.read_bytes())

    minio_mock.download_file.side_effect = fake_download

    publisher_mock = MagicMock()
    pipeline_logger_mock = MagicMock()

    consumer = VoiceProfileConsumer(
        publisher=publisher_mock,
        pipeline_logger=pipeline_logger_mock,
        minio_client=minio_mock,
        chain_factory=_make_mock_chain,
    )

    event = VoiceProfileUploadedEvent(
        trace_id="trace-vp-1",
        voice_profile_id="0192c7b2-1111-7000-8000-000000000001",
        ham_ses_yolu="voices/0192c7b2-1111-7000-8000-000000000001/raw.wav",
        referans_metin="Bu bir deneme kaydıdır.",
        ornek_metin="Merhaba, bu örnek paragraf seslendirmesidir.",
    )
    consumer.handle(event)

    assert minio_mock.upload_file.call_count == 2
    publisher_mock.publish_voice_profile_ready.assert_called_once()
    ready_evt = publisher_mock.publish_voice_profile_ready.call_args[0][0]
    assert ready_evt.basarili is True
    assert ready_evt.referans_ses_yolu == "voices/0192c7b2-1111-7000-8000-000000000001/reference.wav"
    assert ready_evt.ornek_ses_yolu == "voices/0192c7b2-1111-7000-8000-000000000001/sample.ogg"
    assert len(ready_evt.referans_hash) == 64
    assert ready_evt.referans_sure_sn >= 5.0


def test_voice_profile_consumer_reports_error_when_audio_too_short(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.setattr("config.settings.temp_dir", tmp_path / "temp")

    short_wav = write_sine_wav(tmp_path / "short_raw.wav", duration_sec=2.0, sample_rate=24000)

    minio_mock = MagicMock()
    minio_mock.download_file.side_effect = lambda _k, dest: dest.write_bytes(short_wav.read_bytes())

    publisher_mock = MagicMock()
    pipeline_logger_mock = MagicMock()

    consumer = VoiceProfileConsumer(
        publisher=publisher_mock,
        pipeline_logger=pipeline_logger_mock,
        minio_client=minio_mock,
        chain_factory=_make_mock_chain,
    )

    event = VoiceProfileUploadedEvent(
        trace_id="trace-vp-short",
        voice_profile_id="0192c7b2-2222-7000-8000-000000000002",
        ham_ses_yolu="voices/0192c7b2-2222-7000-8000-000000000002/raw.wav",
        referans_metin=None,
        ornek_metin="Örnek metin.",
    )
    consumer.handle(event)

    publisher_mock.publish_voice_profile_ready.assert_called_once()
    ready_evt = publisher_mock.publish_voice_profile_ready.call_args[0][0]
    assert ready_evt.basarili is False
    assert "En az 5 saniyelik" in (ready_evt.hata_mesaji or "")
    publisher_mock.publish_pipeline_error.assert_called_once()
