from __future__ import annotations

from pathlib import Path

from models.book_models import (
    BookMeta,
    ChapterDraft,
    CleanedDocument,
    CleaningStats,
    ExtractedDocument,
    ExtractedPage,
    ParagraphWithStyle,
)
from pipeline.cleaning.scanned_pdf_detector import ScannedPdfDetector
from pipeline.extraction.pdf_extractor import PdfExtractor
from pipeline.structure.chapter_detector import ChapterDetector
from pipeline.structure.chapter_post_processor import ChapterPostProcessor
from pipeline.structure.metadata_resolver import MetadataResolver
from tests.factories.pdf_factory import create_scanned_image_pdf, create_synthetic_book_pdf


def test_chapter_detector_uses_pdf_outline_when_available(tmp_path: Path) -> None:
    spec = create_synthetic_book_pdf(tmp_path / "with_outline.pdf", with_outline=True)
    doc = PdfExtractor().extract(spec.path)
    from pipeline.cleaning.text_cleaner import TextCleaner

    cleaned = TextCleaner().clean(doc)
    detector = ChapterDetector()
    drafts, method = detector.detect(doc, cleaned)

    assert method == "pdf_outline"
    assert len(drafts) == 2
    assert drafts[0].title == "Birinci Bolum"
    assert drafts[1].title == "Ikinci Bolum"


def test_chapter_detector_uses_heading_heuristic_when_no_outline(tmp_path: Path) -> None:
    spec = create_synthetic_book_pdf(
        tmp_path / "no_outline.pdf",
        with_outline=False,
        page_count_per_chapter=4,
    )
    doc = PdfExtractor().extract(spec.path)
    from pipeline.cleaning.text_cleaner import TextCleaner

    cleaned = TextCleaner().clean(doc)
    detector = ChapterDetector()
    drafts, method = detector.detect(doc, cleaned)

    assert method == "heading_heuristic"
    assert len(drafts) == 2
    assert "BIRINCI BOLUM" in drafts[0].title
    assert "IKINCI BOLUM" in drafts[1].title


def test_chapter_detector_falls_back_to_fixed_size() -> None:
    long_par = " ".join(["kelime"] * 350)
    pars = [
        ParagraphWithStyle(text=long_par, font_size=11.0, is_bold=False, page_index=0),
        ParagraphWithStyle(text=long_par, font_size=11.0, is_bold=False, page_index=0),
        ParagraphWithStyle(text=long_par, font_size=11.0, is_bold=False, page_index=0),
    ]
    cleaned = CleanedDocument(paragraphs_by_page=[pars], body_font_size=11.0, stats=CleaningStats())
    doc = ExtractedDocument(
        format="txt",
        pages=[ExtractedPage(index=0, width=595, height=842, blocks=[])],
        toc=[],
        meta=BookMeta(),
    )

    detector = ChapterDetector(pseudo_chapter_words=500)
    drafts, method = detector.detect(doc, cleaned)

    assert method == "fixed_size"
    assert len(drafts) == 3
    assert drafts[0].title == "Bölüm 1"


def test_chapter_post_processor_merges_short_and_marks_excluded_titles() -> None:
    body_350 = " ".join(["kelime"] * 350)
    short_50 = " ".join(["kelime"] * 50)

    drafts = [
        ChapterDraft(title="Giriş", paragraphs=["ISBN 978-975-08-0000-0 Yapı Kredi Yayınları 1. Baskı"]),
        ChapterDraft(title="İÇİNDEKİLER", paragraphs=["Bölüm 1 ... 5", "Bölüm 2 ... 45"]),
        ChapterDraft(title="KÜRK MANTOLU MADONNA VE İSTANBUL", paragraphs=[short_50]),
        ChapterDraft(title="İKİNCİ BÖLÜM", paragraphs=[body_350]),
        ChapterDraft(title="KAYNAKÇA", paragraphs=["1. Kaynak eser A", "2. Kaynak eser B"]),
    ]

    processor = ChapterPostProcessor(min_chapter_words=300)
    processed = processor.process(drafts)

    # Künye ve İçindekiler hariç tutulmuş olmalı
    assert processed[0].included is False
    assert processed[0].exclusion_reason == "Künye / Telif sayfası"
    assert processed[1].included is False
    assert processed[1].exclusion_reason == "İçindekiler başlığı"

    # Kısa 3. bölüm (50 kelime) sonraki dahil bölümle birleşmiş olmalı
    included_chapters = [d for d in processed if d.included]
    assert len(included_chapters) == 1
    assert len(included_chapters[0].paragraphs) == 2

    # Son bölüm (Kaynakça) hariç tutulmuş olmalı ve başlığı Türkçe Başlık Düzenine çevrilmiş olmalı
    assert processed[-1].included is False
    assert processed[-1].title == "Kaynakça"

    # Türkçe başlık düzeni kontrolü
    assert processor.clean_title("KÜRK MANTOLU MADONNA VE İSTANBUL") == "Kürk Mantolu Madonna ve İstanbul"


def test_scanned_pdf_detector_identifies_image_only_pdf(tmp_path: Path) -> None:
    scanned_path = create_scanned_image_pdf(tmp_path / "scanned.pdf", page_count=4)
    doc = PdfExtractor().extract(scanned_path)

    detector = ScannedPdfDetector()
    result = detector.analyze(doc)

    assert result.is_scanned is True
    assert result.scanned_ratio == 1.0


def test_metadata_resolver_detects_turkish_and_cleans_placeholder_title(tmp_path: Path) -> None:
    doc = ExtractedDocument(
        format="pdf",
        pages=[ExtractedPage(index=0, width=595, height=842, blocks=[])],
        toc=[],
        meta=BookMeta(title="Microsoft Word - Belge1.docx", author="Halide Edib Adıvar", year=1922, page_count=210),
    )
    cleaned = CleanedDocument(
        paragraphs_by_page=[
            [
                ParagraphWithStyle(text="ATEŞTEN GÖMLEK", font_size=18.0, is_bold=True, page_index=0),
                ParagraphWithStyle(
                    text="İstanbul ve Anadolu'da bir kurtuluş mücadelesi için insanlar bir araya geldi.",
                    font_size=11.0,
                    is_bold=False,
                    page_index=0,
                ),
            ]
        ],
        body_font_size=11.0,
    )

    resolver = MetadataResolver()
    meta = resolver.resolve(doc, cleaned, source_path=tmp_path / "original.pdf")

    assert meta.baslik == "Ateşten Gömlek"
    assert meta.yazar == "Halide Edib Adıvar"
    assert meta.dil == "tr"
    assert meta.yayin_yili == 1922
    assert meta.sayfa_sayisi == 210


def test_chapter_detector_merges_adjacent_headings_and_rejects_stray_numbers() -> None:
    body_par = " ".join(["kelime"] * 120)
    pars = [
        ParagraphWithStyle(text="Giriş önsöz metni burada yer alır.", font_size=11.0, is_bold=False, page_index=0),
        ParagraphWithStyle(text="BÖLÜM 1", font_size=18.0, is_bold=True, page_index=0),
        ParagraphWithStyle(text="Yolculuk Başlıyor", font_size=16.0, is_bold=True, page_index=0),
        ParagraphWithStyle(text="646", font_size=16.0, is_bold=True, page_index=0),
        ParagraphWithStyle(text=body_par, font_size=11.0, is_bold=False, page_index=0),
        ParagraphWithStyle(text="2.", font_size=18.0, is_bold=True, page_index=1),
        ParagraphWithStyle(text=body_par, font_size=11.0, is_bold=False, page_index=1),
    ]
    cleaned = CleanedDocument(paragraphs_by_page=[pars], body_font_size=11.0, stats=CleaningStats())
    doc = ExtractedDocument(
        format="pdf",
        pages=[ExtractedPage(index=0, width=595, height=842, blocks=[])],
        toc=[],
        meta=BookMeta(),
    )

    drafts, method = ChapterDetector().detect(doc, cleaned)
    assert method == "heading_heuristic"
    titles = [d.title for d in drafts]
    assert "Giriş" in titles
    assert "BÖLÜM 1 — Yolculuk Başlıyor" in titles
    assert "646" not in titles
    assert "2." in titles

