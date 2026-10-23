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
from pipeline.cleaning.footnote_remover import FootnoteRemover


def test_footnote_remover_deletes_footnotes_and_superscripts_but_keeps_numbered_lists() -> None:
    # Gövde bloğu: üst simge span ("1") ve metin içi "[2]" ile "kelime³" içeriyor
    body_spans = [
        TextSpan(
            text="Tarihsel kaynaklara göre bu olay 19. yüzyılda gerçekleşmiştir",
            size=11.0,
            flags=0,
            bbox=(72.0, 200.0, 450.0, 212.0),
        ),
        TextSpan(text="1", size=7.0, flags=1, bbox=(450.0, 198.0, 455.0, 205.0)),
        TextSpan(
            text=" ve kaynaklarda[2] ayrıca belirtilmiştir³.",
            size=11.0,
            flags=0,
            bbox=(455.0, 200.0, 520.0, 212.0),
        ),
    ]
    body_block = TextBlock(
        lines=[TextLine(spans=body_spans, bbox=(72.0, 200.0, 520.0, 212.0))],
        bbox=(72.0, 200.0, 520.0, 212.0),
        page_index=0,
    )

    # Gövdeyle aynı font boyutunda (11pt) numaralı liste öğesi (sayfanın alt kısmında olsa bile silinmemeli!)
    list_span = TextSpan(
        text="1. Birinci madde: Bu bir dipnot değil, gövde içi numaralı listedir.",
        size=11.0,
        flags=0,
        bbox=(72.0, 620.0, 500.0, 632.0),
    )
    list_block = TextBlock(
        lines=[TextLine(spans=[list_span], bbox=(72.0, 620.0, 500.0, 632.0))],
        bbox=(72.0, 620.0, 500.0, 632.0),
        page_index=0,
    )

    # Gerçek dipnot bloğu: alt %35'te (y0=720 > 842*0.65) ve küçük fontlu (8.5pt < 11*0.85)
    fn_span = TextSpan(
        text="1 Bkz. Osmanlı Arşiv Vesikaları, Defter No: 14, s. 88.",
        size=8.5,
        flags=0,
        bbox=(72.0, 720.0, 480.0, 730.0),
    )
    fn_block = TextBlock(
        lines=[TextLine(spans=[fn_span], bbox=(72.0, 720.0, 480.0, 730.0))],
        bbox=(72.0, 720.0, 480.0, 730.0),
        page_index=0,
    )

    page = ExtractedPage(index=0, width=595.0, height=842.0, blocks=[body_block, list_block, fn_block])
    doc = ExtractedDocument(format="pdf", pages=[page], toc=[], meta=BookMeta())
    stats = CleaningStats()

    remover = FootnoteRemover()
    median_size = remover.remove(doc, stats)

    assert median_size == 11.0
    assert stats.deleted_footnotes >= 1
    assert len(doc.pages[0].blocks) == 2

    cleaned_body = doc.pages[0].blocks[0].text
    assert "[2]" not in cleaned_body
    assert "³" not in cleaned_body
    assert "19. yüzyılda gerçekleşmiştir ve kaynaklarda ayrıca belirtilmiştir." in cleaned_body

    kept_list_text = doc.pages[0].blocks[1].text
    assert "1. Birinci madde: Bu bir dipnot değil" in kept_list_text
