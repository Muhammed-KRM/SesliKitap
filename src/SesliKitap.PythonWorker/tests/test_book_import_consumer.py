from __future__ import annotations

import json
from pathlib import Path
from unittest.mock import MagicMock

from consumers.book_import_consumer import BookImportConsumer
from models.events import (
    BookImportRequestedEvent,
    DosyaFormati,
    ProcessedChaptersPayload,
    ProcessingSettingsModel,
)
from pipeline.audio.ffmpeg_runner import FfmpegRunner
from tests.conftest import FakeMinioClient, FakePipelineLogger, FakeRabbitMQPublisher
from tests.factories.epub_factory import create_synthetic_epub
from tests.factories.pdf_factory import create_scanned_image_pdf, create_synthetic_book_pdf


def test_book_import_consumer_processes_pdf_and_publishes_events(
    tmp_path: Path,
    fake_minio: FakeMinioClient,
    fake_publisher: FakeRabbitMQPublisher,
    fake_pipeline_logger: FakePipelineLogger,
) -> None:
    pdf_spec = create_synthetic_book_pdf(tmp_path / "source.pdf", with_outline=True, page_count_per_chapter=3)
    book_id = "0192c7b2-1111-7000-8000-000000000001"
    source_key = f"books/{book_id}/source/original.pdf"
    fake_minio.upload_file(source_key, pdf_spec.path, "application/pdf")

    consumer = BookImportConsumer(
        minio_client=fake_minio,  # type: ignore[arg-type]
        publisher=fake_publisher,  # type: ignore[arg-type]
        pipeline_logger=fake_pipeline_logger,  # type: ignore[arg-type]
    )

    event = BookImportRequestedEvent(
        trace_id="trace-pdf-001",
        book_id=book_id,
        orijinal_dosya_yolu=source_key,
        dosya_formati=DosyaFormati.PDF,
        beklenen_format="pdf",
        ayarlar=ProcessingSettingsModel(min_bolum_kelime=10),
    )

    consumer.handle(event)

    # İlerleme yüzdeleri sırayla yayınlanmış olmalı (2 -> 10 -> 15 -> 20.. -> 65 -> 75 -> 85 -> 90 -> 100)
    percentages = [e.yuzde for e in fake_publisher.stage_events]
    assert percentages[0] == 2
    assert 10 in percentages
    assert 15 in percentages
    assert 65 in percentages
    assert 85 in percentages
    assert 90 in percentages
    assert percentages[-1] == 100

    # MinIO'da chapters.json, extraction-report.json ve cover.jpg bulunmalı
    chapters_key = f"books/{book_id}/processed/chapters.json"
    report_key = f"books/{book_id}/processed/extraction-report.json"
    cover_key = f"books/{book_id}/cover.jpg"

    assert fake_minio.exists(chapters_key)
    assert fake_minio.exists(report_key)
    assert fake_minio.exists(cover_key)

    chapters_payload = fake_minio.read_json(chapters_key, ProcessedChaptersPayload)
    assert isinstance(chapters_payload, ProcessedChaptersPayload)
    assert chapters_payload.book_id == book_id
    assert len(chapters_payload.chapters) == 2
    assert chapters_payload.chapters[0].sira_no == 1
    assert chapters_payload.chapters[0].baslik == "Birinci Bolum"

    # BookProcessedEvent yayınlanmış olmalı
    assert len(fake_publisher.processed_events) == 1
    proc_evt = fake_publisher.processed_events[0]
    assert proc_evt.book_id == book_id
    assert proc_evt.ocr_gerekli is False
    assert proc_evt.bolumler_yolu == chapters_key
    assert proc_evt.kapak_yolu == cover_key
    assert proc_evt.istatistik is not None
    assert proc_evt.istatistik.bolum_tespit_yontemi == "pdf_outline"


def test_book_import_consumer_processes_epub(
    tmp_path: Path,
    fake_minio: FakeMinioClient,
    fake_publisher: FakeRabbitMQPublisher,
    fake_pipeline_logger: FakePipelineLogger,
) -> None:
    epub_path = create_synthetic_epub(tmp_path / "source.epub", include_cover=True)
    book_id = "0192c7b2-2222-7000-8000-000000000002"
    source_key = f"books/{book_id}/source/original.epub"
    fake_minio.upload_file(source_key, epub_path, "application/epub+zip")

    consumer = BookImportConsumer(
        minio_client=fake_minio,  # type: ignore[arg-type]
        publisher=fake_publisher,  # type: ignore[arg-type]
        pipeline_logger=fake_pipeline_logger,  # type: ignore[arg-type]
    )

    event = BookImportRequestedEvent(
        trace_id="trace-epub-001",
        book_id=book_id,
        orijinal_dosya_yolu=source_key,
        dosya_formati=DosyaFormati.EPUB,
        beklenen_format="epub",
        ayarlar=ProcessingSettingsModel(min_bolum_kelime=5),
    )

    consumer.handle(event)

    assert len(fake_publisher.processed_events) == 1
    proc_evt = fake_publisher.processed_events[0]
    assert proc_evt.ocr_gerekli is False
    assert proc_evt.meta is not None
    assert proc_evt.meta.baslik == "Kürk Mantolu Test Romanı"


def test_book_import_consumer_detects_scanned_pdf_and_requests_ocr(
    tmp_path: Path,
    fake_minio: FakeMinioClient,
    fake_publisher: FakeRabbitMQPublisher,
    fake_pipeline_logger: FakePipelineLogger,
) -> None:
    scanned_path = create_scanned_image_pdf(tmp_path / "scanned.pdf", page_count=4)
    book_id = "0192c7b2-3333-7000-8000-000000000003"
    source_key = f"books/{book_id}/source/original.pdf"
    fake_minio.upload_file(source_key, scanned_path, "application/pdf")

    consumer = BookImportConsumer(
        minio_client=fake_minio,  # type: ignore[arg-type]
        publisher=fake_publisher,  # type: ignore[arg-type]
        pipeline_logger=fake_pipeline_logger,  # type: ignore[arg-type]
    )

    event = BookImportRequestedEvent(
        trace_id="trace-ocr-001",
        book_id=book_id,
        orijinal_dosya_yolu=source_key,
        dosya_formati=DosyaFormati.PDF,
        beklenen_format="pdf",
    )

    consumer.handle(event)

    assert len(fake_publisher.processed_events) == 1
    proc_evt = fake_publisher.processed_events[0]
    assert proc_evt.book_id == book_id
    assert proc_evt.ocr_gerekli is True
    assert proc_evt.bolumler_yolu is None


def test_base_consumer_on_message_routes_invalid_json_to_dlq(
    fake_minio: FakeMinioClient,
    fake_publisher: FakeRabbitMQPublisher,
    fake_pipeline_logger: FakePipelineLogger,
) -> None:
    consumer = BookImportConsumer(
        minio_client=fake_minio,  # type: ignore[arg-type]
        publisher=fake_publisher,  # type: ignore[arg-type]
        pipeline_logger=fake_pipeline_logger,  # type: ignore[arg-type]
    )
    mock_ch = MagicMock()
    mock_method = MagicMock()
    mock_method.delivery_tag = 42

    consumer._on_message(mock_ch, mock_method, None, b"{corrupted-json")

    mock_ch.basic_nack.assert_called_once_with(delivery_tag=42, requeue=False)
    mock_ch.basic_ack.assert_not_called()


def test_base_consumer_on_message_handles_processing_error_and_acks(
    tmp_path: Path,
    fake_minio: FakeMinioClient,
    fake_publisher: FakeRabbitMQPublisher,
    fake_pipeline_logger: FakePipelineLogger,
) -> None:
    encrypted_pdf = create_synthetic_book_pdf(
        tmp_path / "encrypted.pdf",
        encrypt_password="secret",
    )
    book_id = "0192c7b2-4444-7000-8000-000000000004"
    source_key = f"books/{book_id}/source/original.pdf"
    fake_minio.upload_file(source_key, encrypted_pdf.path, "application/pdf")

    consumer = BookImportConsumer(
        minio_client=fake_minio,  # type: ignore[arg-type]
        publisher=fake_publisher,  # type: ignore[arg-type]
        pipeline_logger=fake_pipeline_logger,  # type: ignore[arg-type]
    )
    mock_ch = MagicMock()
    mock_method = MagicMock()
    mock_method.delivery_tag = 99

    body = json.dumps(
        {
            "bookId": book_id,
            "orijinalDosyaYolu": source_key,
            "dosyaFormati": 0,
            "beklenenFormat": "pdf",
            "traceId": "trace-err-001",
        }
    ).encode("utf-8")

    consumer._on_message(mock_ch, mock_method, None, body)

    # Beklenen iş hatasında PipelineErrorEvent yayınlanmalı ve mesaj ack edilmeli (tekrar denemenin anlamı yok)
    mock_ch.basic_ack.assert_called_once_with(delivery_tag=99)
    assert len(fake_publisher.error_events) == 1
    assert fake_publisher.error_events[0].hata_kodu == "PDF_ENCRYPTED"
    assert fake_publisher.error_events[0].book_id == book_id


def test_ffmpeg_runner_constructs_safe_command() -> None:
    runner = FfmpegRunner(ffmpeg_bin="ffmpeg", ffprobe_bin="ffprobe")
    assert runner.ffmpeg_bin == "ffmpeg"
    assert runner.ffprobe_bin == "ffprobe"
