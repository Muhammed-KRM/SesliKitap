from __future__ import annotations

from pathlib import Path

import pytest
from rapidfuzz import fuzz

from models.events import ProcessingError
from pipeline.cleaning.text_cleaner import TextCleaner
from pipeline.extraction import get_extractor
from pipeline.extraction.pdf_extractor import PdfExtractor
from tests.factories.pdf_factory import create_synthetic_book_pdf


def test_pdf_extractor_extracts_pages_toc_and_cover(tmp_path: Path) -> None:
    pdf_path = tmp_path / "book.pdf"
    spec = create_synthetic_book_pdf(pdf_path, with_outline=True, page_count_per_chapter=3)

    extractor = PdfExtractor()
    doc = extractor.extract(spec.path)

    assert doc.format == "pdf"
    assert len(doc.pages) == 6
    assert len(doc.toc) == 2
    assert doc.toc[0].title == "Birinci Bolum"
    assert doc.toc[0].page_index == 0
    assert doc.toc[1].title == "Ikinci Bolum"
    assert doc.toc[1].page_index == 3
    assert doc.meta.title == "Sentetik Test Kitabi"
    assert doc.meta.author == "Ahmet Test Yazar"
    assert doc.cover_bytes is not None
    assert len(doc.cover_bytes) > 500


def test_pdf_extractor_end_to_end_cleaning_zero_leakage(tmp_path: Path) -> None:
    pdf_path = tmp_path / "clean_e2e.pdf"
    spec = create_synthetic_book_pdf(
        pdf_path,
        with_outline=True,
        page_count_per_chapter=3,
        include_headers=True,
        include_footnotes=True,
    )

    doc = get_extractor("pdf").extract(spec.path)
    cleaner = TextCleaner()
    cleaned = cleaner.clean(doc)

    # Üst bilgi, sayfa numarası ve dipnotlar silinmiş olmalı
    assert cleaned.stats.deleted_headers >= 5
    assert cleaned.stats.deleted_page_numbers >= 5
    assert cleaned.stats.deleted_footnotes >= 5
    assert cleaned.stats.merged_hyphens >= 6

    # Başlık paragrafları hariç gövde paragraflarını birleştir
    body_paragraphs = [
        p.text
        for p in cleaned.all_paragraphs
        if p.text not in ("BIRINCI BOLUM", "IKINCI BOLUM")
    ]
    actual_text = "\n\n".join(body_paragraphs)

    # Sızıntı kontrolü: üst bilgi veya dipnot metni gövdede hiç geçmemeli
    assert spec.header_text not in actual_text
    assert "Arsiv Belgeleri" not in actual_text
    assert "[1]" not in actual_text

    similarity = fuzz.ratio(actual_text, spec.expected_full_text)
    assert similarity >= 99.0


def test_pdf_extractor_rejects_encrypted_pdf(tmp_path: Path) -> None:
    pdf_path = tmp_path / "encrypted.pdf"
    create_synthetic_book_pdf(pdf_path, encrypt_password="secret-password")

    extractor = PdfExtractor()
    with pytest.raises(ProcessingError) as exc_info:
        extractor.extract(pdf_path)

    assert exc_info.value.code == "PDF_ENCRYPTED"


def test_pdf_extractor_rejects_corrupted_file(tmp_path: Path) -> None:
    bad_path = tmp_path / "corrupt.pdf"
    bad_path.write_bytes(b"%PDF-1.4 broken binary content")

    extractor = PdfExtractor()
    with pytest.raises(ProcessingError) as exc_info:
        extractor.extract(bad_path)

    assert exc_info.value.code == "PDF_OPEN_FAILED"
