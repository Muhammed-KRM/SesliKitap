"""Faz 8 Python Worker testleri: M4B üretimi, ExportConsumer, OcrProcessor ve kullanıcı telaffuz önceliği."""

from __future__ import annotations

import subprocess
from pathlib import Path
from types import SimpleNamespace

import fitz
import numpy as np
import pytest
import soundfile as sf

import pipeline.extraction.ocr_processor as ocr_module
from consumers.book_import_consumer import BookImportConsumer
from consumers.export_consumer import ExportConsumer
from models.events import (
    BookExportChapterItem,
    BookExportRequestedEvent,
    BookImportRequestedEvent,
    DosyaFormati,
    ProcessingSettingsModel,
)
from pipeline.audio.ffmpeg_runner import FfmpegRunner
from pipeline.audio.m4b_builder import M4bBuilder, M4bChapterInput
from pipeline.extraction.ocr_processor import OcrProcessor
from pipeline.text.pronunciation_applier import PronunciationApplier, PronunciationEntry
from tests.conftest import FakeMinioClient, FakeRabbitMQPublisher
from tests.factories.pdf_factory import create_synthetic_book_pdf


class FakeM4bFfmpegRunner(FfmpegRunner):
    """FFmpeg kurulu olmayan yerel test ortamlarında M4B üretim argümanlarını doğrulayan sahte runner."""

    def __init__(self) -> None:
        super().__init__()
        self.recorded_args: list[list[str]] = []

    def run(
        self,
        args: list[str],
        *,
        timeout_sec: int = 600,
        log_level: str = "error",
    ) -> subprocess.CompletedProcess[str]:
        self.recorded_args.append(args)
        out_path = Path(args[-1])
        out_path.parent.mkdir(parents=True, exist_ok=True)
        out_path.write_bytes(b"\x00\x00\x00\x18ftypM4A \x00\x00\x00\x00M4A mp42isom" + (b"\x00" * 2048))
        return subprocess.CompletedProcess(args=args, returncode=0, stdout="", stderr="")


def _write_test_wav(path: Path, duration_sec: float = 1.0, sr: int = 24000) -> Path:
    t = np.linspace(0, duration_sec, int(sr * duration_sec), endpoint=False, dtype=np.float32)
    samples = 0.2 * np.sin(2 * np.pi * 440.0 * t).astype(np.float32)
    sf.write(str(path), samples, sr, format="WAV", subtype="PCM_16")
    return path


def test_m4b_builder_creates_chaptered_audiobook_and_ffmetadata(tmp_path: Path) -> None:
    ch1 = _write_test_wav(tmp_path / "ch1.wav", duration_sec=1.2)
    ch2 = _write_test_wav(tmp_path / "ch2.wav", duration_sec=1.5)
    cover = tmp_path / "cover.jpg"
    cover.write_bytes(b"\xff\xd8\xff\xe0JFIF")
    out_m4b = tmp_path / "kitap.m4b"
    work_dir = tmp_path / "work"

    fake_ffmpeg = FakeM4bFfmpegRunner()
    builder = M4bBuilder(ffmpeg=fake_ffmpeg)
    size_bytes = builder.build(
        [
            M4bChapterInput(sira_no=1, baslik="Birinci Bölüm: Giriş", audio_path=ch1, duration_sec=1.2),
            M4bChapterInput(sira_no=2, baslik="İkinci Bölüm: Gelişme", audio_path=ch2, duration_sec=1.5),
        ],
        out_m4b,
        book_title="Kürk Mantolu Madonna",
        author="Sabahattin Ali",
        cover_path=cover,
        work_dir=work_dir,
    )

    assert size_bytes > 1000
    assert out_m4b.is_file()

    ffmetadata_text = (work_dir / "ffmetadata.txt").read_text(encoding="utf-8")
    assert ";FFMETADATA1" in ffmetadata_text
    assert "title=Kürk Mantolu Madonna" in ffmetadata_text
    assert "artist=Sabahattin Ali" in ffmetadata_text
    assert "title=Birinci Bölüm: Giriş" in ffmetadata_text
    assert "title=İkinci Bölüm: Gelişme" in ffmetadata_text
    assert "START=0" in ffmetadata_text
    assert "END=1200" in ffmetadata_text
    assert "START=1200" in ffmetadata_text
    assert "END=2700" in ffmetadata_text


def test_export_consumer_uploads_m4b_and_publishes_completed_event(
    tmp_path: Path,
    fake_minio: FakeMinioClient,
    fake_publisher: FakeRabbitMQPublisher,
    fake_pipeline_logger: object,
) -> None:
    wav_path = _write_test_wav(tmp_path / "c1.wav", duration_sec=1.0)
    fake_minio.upload_bytes("books/b1/audio/c1/v1/chapter.wav", wav_path.read_bytes())

    fake_ffmpeg = FakeM4bFfmpegRunner()
    consumer = ExportConsumer(
        minio_client=fake_minio,  # type: ignore[arg-type]
        publisher=fake_publisher,  # type: ignore[arg-type]
        pipeline_logger=fake_pipeline_logger,  # type: ignore[arg-type]
        m4b_builder=M4bBuilder(ffmpeg=fake_ffmpeg),
    )

    msg = BookExportRequestedEvent(
        trace_id="trace-m4b",
        book_id="b1",
        baslik="Sefiller",
        yazar="Victor Hugo",
        cikti_yolu="books/b1/export/sefiller.m4b",
        bolumler=[
            BookExportChapterItem(
                sira_no=1,
                baslik="1. Bölüm",
                ses_yolu="books/b1/audio/c1/v1/chapter.wav",
                sure_sn=1.0,
            )
        ],
    )

    consumer.handle(msg)

    assert fake_minio.exists("books/b1/export/sefiller.m4b")
    assert len(fake_publisher.export_completed_events) == 1
    completed = fake_publisher.export_completed_events[0]
    assert completed.basarili is True
    assert completed.book_id == "b1"
    assert completed.boyut > 0


def test_ocr_processor_and_book_import_ocr_branch(
    monkeypatch: pytest.MonkeyPatch,
    fake_minio: FakeMinioClient,
    fake_publisher: FakeRabbitMQPublisher,
    fake_pipeline_logger: object,
) -> None:
    def _fake_ocr(input_file: str, output_file: str, **_kwargs: object) -> None:
        create_synthetic_book_pdf(Path(output_file), with_outline=True, page_count_per_chapter=2)

    monkeypatch.setattr(ocr_module, "_ocrmypdf", SimpleNamespace(ocr=_fake_ocr))

    raw_doc = fitz.open()
    raw_doc.new_page()
    fake_minio.upload_bytes("books/b-ocr/source/original.pdf", raw_doc.tobytes())
    raw_doc.close()

    consumer = BookImportConsumer(
        minio_client=fake_minio,  # type: ignore[arg-type]
        publisher=fake_publisher,  # type: ignore[arg-type]
        pipeline_logger=fake_pipeline_logger,  # type: ignore[arg-type]
        ocr_processor=OcrProcessor(),
    )

    consumer.handle(
        BookImportRequestedEvent(
            trace_id="trace-ocr",
            book_id="b-ocr",
            orijinal_dosya_yolu="books/b-ocr/source/original.pdf",
            dosya_formati=DosyaFormati.PDF,
            beklenen_format="pdf",
            ayarlar=ProcessingSettingsModel(ocr_uygula=True),
        )
    )

    assert len(fake_publisher.processed_events) == 1
    evt = fake_publisher.processed_events[0]
    assert evt.ocr_gerekli is False
    assert evt.bolumler_yolu == "books/b-ocr/processed/chapters.json"


def test_user_pronunciation_entry_overrides_default_lexicon() -> None:
    applier = PronunciationApplier(
        user_entries=[
            PronunciationEntry(
                word="Shakespeare",
                spoken="Şeykspir",
                case_sensitive=False,
            )
        ]
    )
    out = applier.apply("William Shakespeare eserleri sahnelendi.")
    assert "Şeykspir" in out
