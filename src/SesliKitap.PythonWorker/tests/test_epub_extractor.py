from __future__ import annotations

import zipfile
from pathlib import Path

import pytest

from models.events import ProcessingError
from pipeline.cleaning.text_cleaner import TextCleaner
from pipeline.extraction.epub_extractor import EpubExtractor
from tests.factories.epub_factory import create_synthetic_epub


def test_epub_extractor_extracts_sections_toc_and_removes_footnotes(tmp_path: Path) -> None:
    epub_path = create_synthetic_epub(tmp_path / "roman.epub", include_cover=True)

    extractor = EpubExtractor()
    doc = extractor.extract(epub_path)

    assert doc.format == "epub"
    assert doc.meta.title == "Kürk Mantolu Test Romanı"
    assert doc.meta.author == "Sabahattin Test"
    assert len(doc.toc) >= 2
    assert doc.cover_bytes is not None

    cleaner = TextCleaner()
    cleaned = cleaner.clean(doc)
    full_text = "\n".join(p.text for p in cleaned.all_paragraphs)

    assert "Şimdiye kadar tesadüf ettiğim insanlardan" in full_text
    assert "Berlin sokaklarında soğuk bir kış akşamı" in full_text
    # Dipnot aside metni temizlenmiş olmalı
    assert "Editörün dipnot açıklaması" not in full_text


def test_epub_extractor_blocks_zip_path_traversal(tmp_path: Path) -> None:
    evil_epub = tmp_path / "evil.epub"
    with zipfile.ZipFile(evil_epub, "w") as zf:
        zf.writestr("../../etc/passwd", "root:x:0:0")

    extractor = EpubExtractor()
    with pytest.raises(ProcessingError) as exc_info:
        extractor.extract(evil_epub)

    assert exc_info.value.code == "EPUB_INVALID_PATH"


def test_epub_extractor_rejects_invalid_zip(tmp_path: Path) -> None:
    bad_epub = tmp_path / "not_a_zip.epub"
    bad_epub.write_bytes(b"this is not a valid zip archive")

    extractor = EpubExtractor()
    with pytest.raises(ProcessingError) as exc_info:
        extractor.extract(bad_epub)

    assert exc_info.value.code == "EPUB_CORRUPT_ARCHIVE"
