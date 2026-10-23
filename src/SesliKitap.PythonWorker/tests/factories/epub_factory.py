from __future__ import annotations

from pathlib import Path

import fitz
from ebooklib import epub


def create_synthetic_epub(target_path: Path, *, include_cover: bool = True) -> Path:
    """TOC, kapak görseli ve dipnot işaretleri içeren sentetik Türkçe EPUB üretir."""
    book = epub.EpubBook()
    book.set_identifier("id-seslikitap-test-001")
    book.set_title("Kürk Mantolu Test Romanı")
    book.set_language("tr")
    book.add_author("Sabahattin Test")

    if include_cover:
        pix = fitz.Pixmap(fitz.csRGB, fitz.IRect(0, 0, 300, 450), False)
        pix.clear_with(180)
        book.set_cover("cover.jpg", pix.tobytes("jpeg"), create_page=False)

    ch1 = epub.EpubHtml(title="Birinci Bölüm", file_name="chap_01.xhtml", lang="tr")
    ch1.content = """
    <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
      <head><title>Birinci Bölüm</title></head>
      <body>
        <h1>Birinci Bölüm</h1>
        <p>Şimdiye kadar tesadüf ettiğim insanlardan bir tanesi benim üzerimde belki en büyük tesiri yapmıştır.<a epub:type="noteref" href="#fn1">1</a></p>
        <p>Aradan aylar geçtiği halde bu tesir hiçbir şekilde eksilmedi ve her geçen gün daha da derinleşti.</p>
        <aside id="fn1" epub:type="footnote"><p>1. Editörün dipnot açıklaması burada yer alır.</p></aside>
      </body>
    </html>
    """.encode()

    ch2 = epub.EpubHtml(title="İkinci Bölüm", file_name="chap_02.xhtml", lang="tr")
    ch2.content = """
    <html xmlns="http://www.w3.org/1999/xhtml">
      <head><title>İkinci Bölüm</title></head>
      <body>
        <h1>İkinci Bölüm</h1>
        <p>Berlin sokaklarında soğuk bir kış akşamı yürürken sanat galerisinin önünden geçiyordum.</p>
        <p>Vitrinin arkasındaki tablolar sessizce yoldan geçen insanları seyrediyor gibiydi.</p>
      </body>
    </html>
    """.encode()

    book.add_item(ch1)
    book.add_item(ch2)

    book.toc = (
        epub.Link("chap_01.xhtml", "Birinci Bölüm", "ch1"),
        epub.Link("chap_02.xhtml", "İkinci Bölüm", "ch2"),
    )
    book.add_item(epub.EpubNcx())
    book.add_item(epub.EpubNav())
    book.spine = ["nav", ch1, ch2]

    target_path.parent.mkdir(parents=True, exist_ok=True)
    epub.write_epub(str(target_path), book, {})
    return target_path
