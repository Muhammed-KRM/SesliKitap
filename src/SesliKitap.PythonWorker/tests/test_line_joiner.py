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


def test_line_joiner_reconstructs_paragraphs_from_fragmented_single_line_blocks_and_stitches_drop_cap() -> None:
    # Sayfadaki her satırın ayrı blok olarak geldiği PDF düzenini simüle et
    blocks = [
        # Drop cap 'O'
        TextBlock(lines=[_line("O", 85.0, 140.0, 114.0, 145.0, size=44.0)], bbox=(114.0, 85.0, 145.0, 140.0), page_index=0),
        # Paragraf 1 satırları
        TextBlock(lines=[_line("yun sektörüne ilişkin ilgimizin başlangıcı, 2019 yılında yaptığımız bir araştırma-", 94.0, 105.0)], bbox=(146.0, 94.0, 430.0, 105.0), page_index=0),
        TextBlock(lines=[_line("ya dayanıyor. İzmir Kalkınma Ajansı olarak 2019 yılı boyunca İzmir'i merkeze", 107.0, 118.0)], bbox=(146.0, 107.0, 430.0, 118.0), page_index=0),
        TextBlock(lines=[_line("alan çok sayıda araştırma yaptık. Yaratıcı endüstrileri konu edindiğimiz araş-", 120.0, 131.0)], bbox=(146.0, 120.0, 430.0, 131.0), page_index=0),
        TextBlock(lines=[_line("tırmamız da bunlardan bir tanesiydi. Alanında öncü ve birçok açıdan ilk olma özelliği", 133.0, 144.0)], bbox=(116.0, 133.0, 430.0, 144.0), page_index=0),
        TextBlock(lines=[_line("taşıyan bu çalışmamız, İzmir'in yaratıcı endüstriler açısından sahip olduğu potan-", 146.0, 157.0)], bbox=(116.0, 146.0, 430.0, 157.0), page_index=0),
        TextBlock(lines=[_line("siyeli ortaya koyarken özellikle oyun sektörüne özel bir ilgi gösterilmesi gerektiğini", 159.0, 170.0)], bbox=(116.0, 159.0, 430.0, 170.0), page_index=0),
        TextBlock(lines=[_line("veriye dayalı olarak işaret ediyordu. Hal böylece olunca, 2020 yılında oyun sektörünü", 172.0, 183.0)], bbox=(116.0, 172.0, 430.0, 183.0), page_index=0),
        TextBlock(lines=[_line("Ajansımızın temel çalışma alanları arasına dâhil ettik.", 185.0, 196.0, x1=342.0)], bbox=(116.0, 185.0, 342.0, 196.0), page_index=0),
        # Paragraf 2 satırları (belirgin dikey boşlukla başlar ve nokta sonrasıdır)
        TextBlock(lines=[_line("Yaptığımız ilk iş, oyun sektörünü tanımak ve öğrenmekti. Bu kapsamda, masa başı", 200.0, 211.0)], bbox=(116.0, 200.0, 430.0, 211.0), page_index=0),
        TextBlock(lines=[_line("çalışmalarının yanı sıra etkinliklere katılım sağlayarak sektörün dinamiklerini anlamaya", 213.0, 224.0)], bbox=(116.0, 213.0, 430.0, 224.0), page_index=0),
        TextBlock(lines=[_line("çalıştık.", 226.0, 237.0, x1=200.0)], bbox=(116.0, 226.0, 200.0, 237.0), page_index=0),
    ]

    page = ExtractedPage(index=0, width=595.0, height=842.0, blocks=blocks)
    doc = ExtractedDocument(format="pdf", pages=[page], toc=[], meta=BookMeta())
    stats = CleaningStats()

    joiner = LineJoiner()
    pars_by_page = joiner.join_document(doc, body_font_size=8.8, stats=stats)

    # 12 ayrı blok tam olarak 2 düzenli paragrafa dönüşmeli
    assert len(pars_by_page[0]) == 2
    par1 = pars_by_page[0][0].text
    par2 = pars_by_page[0][1].text

    assert par1.startswith("Oyun sektörüne ilişkin ilgimizin başlangıcı")
    assert "İzmir'i merkeze alan çok sayıda" in par1
    assert "ilk olma özelliği taşıyan bu" in par1
    assert "oyun sektörünü Ajansımızın temel çalışma alanları arasına dâhil ettik." in par1
    assert par1.endswith("dâhil ettik.")

    assert par2.startswith("Yaptığımız ilk iş, oyun sektörünü")
    assert par2.endswith("çalıştık.")
