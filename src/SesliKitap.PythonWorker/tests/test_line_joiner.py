from __future__ import annotations

from models.book_models import (
    BookMeta,
    CleaningStats,
    ExtractedDocument,
    ExtractedPage,
    TextBlock,
    TextLine,
    TextSpan,
)
from pipeline.cleaning.line_joiner import LineJoiner


def _line(text: str, y0: float, y1: float, x0: float = 72.0, x1: float = 500.0, size: float = 11.0) -> TextLine:
    return TextLine(
        spans=[TextSpan(text=text, size=size, flags=0, bbox=(x0, y0, x1, y1))],
        bbox=(x0, y0, x1, y1),
    )


def test_line_joiner_merges_turkish_hyphens_and_preserves_proper_noun_hyphen() -> None:
    lines = [
        _line("Kütüphanedeki eski kitap-", 100.0, 112.0),
        _line("lar özenle raflara dizildi ve Türk-", 114.0, 126.0),
        _line("İslam sentezini anlatan ﬁlozof eserleri ayrıldı.", 128.0, 140.0),
    ]
    block = TextBlock(lines=lines, bbox=(72.0, 100.0, 500.0, 140.0), page_index=0)
    page = ExtractedPage(index=0, width=595.0, height=842.0, blocks=[block])
    doc = ExtractedDocument(format="pdf", pages=[page], toc=[], meta=BookMeta())
    stats = CleaningStats()

    joiner = LineJoiner()
    pars_by_page = joiner.join_document(doc, body_font_size=11.0, stats=stats)

    assert len(pars_by_page[0]) == 1
    text = pars_by_page[0][0].text
    assert "kitaplar özenle" in text
    assert "Türk-İslam sentezini" in text
    assert "filozof eserleri" in text
    assert stats.merged_hyphens == 2


def test_line_joiner_merges_paragraphs_across_pages_and_separates_dialogues() -> None:
    p1_lines = [
        _line("Uzun cümlenin ilk kısmı sayfa sonunda noktalamasız biterken", 700.0, 712.0),
    ]
    p2_lines = [
        _line("sonraki sayfanın başında küçük harfle devam etmektedir.", 100.0, 112.0),
        _line("— Merhaba dostum, nereye gidiyorsun?", 130.0, 142.0),
        _line("— Kütüphaneye doğru yürüyorum.", 144.0, 156.0),
    ]

    page1 = ExtractedPage(
        index=0,
        width=595.0,
        height=842.0,
        blocks=[TextBlock(lines=p1_lines, bbox=(72.0, 700.0, 500.0, 712.0), page_index=0)],
    )
    page2 = ExtractedPage(
        index=1,
        width=595.0,
        height=842.0,
        blocks=[TextBlock(lines=p2_lines, bbox=(72.0, 100.0, 500.0, 156.0), page_index=1)],
    )
    doc = ExtractedDocument(format="pdf", pages=[page1, page2], toc=[], meta=BookMeta())
    stats = CleaningStats()

    joiner = LineJoiner()
    pars_by_page = joiner.join_document(doc, body_font_size=11.0, stats=stats)

    assert len(pars_by_page[0]) == 1
    assert (
        pars_by_page[0][0].text
        == "Uzun cümlenin ilk kısmı sayfa sonunda noktalamasız biterken sonraki sayfanın başında küçük harfle devam etmektedir."
    )
    # Diyalog satırları ayrı paragraf olarak kalmalı
    assert len(pars_by_page[1]) == 2
    assert pars_by_page[1][0].text.startswith("— Merhaba")
    assert pars_by_page[1][1].text.startswith("— Kütüphaneye")
