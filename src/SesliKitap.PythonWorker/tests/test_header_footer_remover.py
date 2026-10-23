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
from pipeline.cleaning.header_footer_remover import HeaderFooterRemover


def _make_line(text: str, y0: float, y1: float, size: float = 10.0) -> TextLine:
    span = TextSpan(text=text, size=size, flags=0, bbox=(50.0, y0, 500.0, y1))
    return TextLine(spans=[span], bbox=(50.0, y0, 500.0, y1))


def test_header_footer_remover_deletes_repeating_headers_and_4_page_number_formats() -> None:
    pages: list[ExtractedPage] = []
    page_num_formats = ["1", "- 2 -", "Sayfa 3", "iv", "5", "s. 6"]

    for idx in range(6):
        header_text = "Sefiller - Victor Hugo" if idx % 2 == 0 else f"Birinci Kisim — {idx + 1}"
        header_line = _make_line(header_text, 20.0, 32.0, size=9.0)
        # Gövdede kitap adı geçen normal bir cümle (silinmemeli!)
        body_line = _make_line(
            f"Bu sayfada Sefiller - Victor Hugo eseri uzerine inceleme yapilmaktadir ({idx + 1}).",
            300.0,
            315.0,
            size=11.0,
        )
        footer_line = _make_line(page_num_formats[idx], 790.0, 805.0, size=9.0)

        blocks = [
            TextBlock(lines=[header_line], bbox=(50.0, 20.0, 500.0, 32.0), page_index=idx),
            TextBlock(lines=[body_line], bbox=(50.0, 300.0, 500.0, 315.0), page_index=idx),
            TextBlock(lines=[footer_line], bbox=(50.0, 790.0, 500.0, 805.0), page_index=idx),
        ]
        pages.append(ExtractedPage(index=idx, width=595.0, height=842.0, blocks=blocks))

    doc = ExtractedDocument(format="pdf", pages=pages, toc=[], meta=BookMeta())
    stats = CleaningStats()

    remover = HeaderFooterRemover(region_ratio=0.08, repeat_threshold=0.30)
    remover.remove(doc, stats)

    assert stats.deleted_headers == 6
    assert stats.deleted_page_numbers == 6

    for page in doc.pages:
        assert len(page.blocks) == 1
        remaining_text = page.blocks[0].text
        assert "uzerine inceleme yapilmaktadir" in remaining_text
