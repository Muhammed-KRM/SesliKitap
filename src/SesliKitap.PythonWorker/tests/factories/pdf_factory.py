from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import fitz


@dataclass(slots=True)
class GeneratedPdfSpec:
    """Programatik üretilen test PDF'i ve beklenen temiz metin bilgisi."""

    path: Path
    expected_chapters: dict[str, str]
    expected_full_text: str
    header_text: str


def create_synthetic_book_pdf(
    target_path: Path,
    *,
    with_outline: bool = True,
    page_count_per_chapter: int = 3,
    include_headers: bool = True,
    include_footnotes: bool = True,
    encrypt_password: str | None = None,
) -> GeneratedPdfSpec:
    """Üst bilgi, sayfa numarası, tireli kelimeler, dipnot ve bölümler içeren sentetik PDF üretir."""
    doc = fitz.open()
    doc.set_metadata(
        {
            "title": "Sentetik Test Kitabi",
            "author": "Ahmet Test Yazar",
            "subject": "SesliKitap Birim Testi",
        }
    )

    header_text = "Sentetik Test Kitabi - Ahmet Test Yazar"
    chapters = [
        ("BIRINCI BOLUM", "Karanlik gecede uzun yolculuk basladi."),
        ("IKINCI BOLUM", "Sabah gunesi dogarken kapilar acildi."),
    ]

    toc: list[list[int | str]] = []
    expected_chapters: dict[str, str] = {}
    current_page_num = 0

    for ch_idx, (ch_title, base_sentence) in enumerate(chapters):
        chapter_start_page = current_page_num + 1
        toc.append([1, ch_title.title(), chapter_start_page])
        chapter_paragraphs: list[str] = []

        for p_offset in range(page_count_per_chapter):
            current_page_num += 1
            page = doc.new_page(width=595, height=842)

            if include_headers:
                # Üst bilgi (y < 842 * 0.08 = 67.36)
                page.insert_text(
                    (50, 35),
                    f"{header_text}   {current_page_num}",
                    fontsize=9,
                    fontname="helv",
                )
                # Alt bilgi sayfa numarası (y > 842 * 0.92 = 774.64)
                footer_label = f"- {current_page_num} -" if current_page_num % 2 == 0 else f"{current_page_num}"
                page.insert_text(
                    (280, 810),
                    footer_label,
                    fontsize=9,
                    fontname="helv",
                )

            y_cursor = 110.0
            if p_offset == 0:
                # Bölüm başlığı (büyük font: 18pt >= 11pt * 1.3)
                page.insert_text(
                    (72, y_cursor),
                    ch_title,
                    fontsize=18,
                    fontname="helv",
                )
                y_cursor += 45.0

            # Gövde paragrafı 1 (tireli kelime içerir: "bilgisayar-\nlar" -> "bilgisayarlar")
            line1 = f"{base_sentence} Sayfa {current_page_num} uzerinde bulunan eski bilgisayar-"
            line2 = "lar hala sorunsuz sekilde calismaya devam ediyordu."
            full_par1 = f"{base_sentence} Sayfa {current_page_num} uzerinde bulunan eski bilgisayarlar hala sorunsuz sekilde calismaya devam ediyordu."
            chapter_paragraphs.append(full_par1)

            page.insert_text((72, y_cursor), line1, fontsize=11, fontname="helv")
            page.insert_text((72, y_cursor + 14), line2, fontsize=11, fontname="helv")
            y_cursor += 55.0

            # Gövde paragrafı 2 (köşeli parantez dipnot referansı içerir: "[1]" silinmeli)
            raw_par2 = f"Bu ikinci paragrafta onemli bir kaynak[1] incelenmektedir ({ch_idx + 1})."
            clean_par2 = f"Bu ikinci paragrafta onemli bir kaynak incelenmektedir ({ch_idx + 1})."
            chapter_paragraphs.append(clean_par2)
            page.insert_text((72, y_cursor), raw_par2, fontsize=11, fontname="helv")

            if include_footnotes:
                # Sayfanın alt %35'inde (y > 842 * 0.65 = 547.3) küçük fontlu (8pt < 11 * 0.85) dipnot
                page.insert_text(
                    (72, 710),
                    f"1Bkz. Arsiv Belgeleri Cilt {current_page_num}, s. 42.",
                    fontsize=8,
                    fontname="helv",
                )

        expected_chapters[ch_title.title()] = "\n\n".join(chapter_paragraphs)

    if with_outline:
        doc.set_toc(toc)

    target_path.parent.mkdir(parents=True, exist_ok=True)
    if encrypt_password:
        doc.save(
            str(target_path),
            encryption=fitz.PDF_ENCRYPT_AES_256,
            owner_pw=encrypt_password,
            user_pw=encrypt_password,
        )
    else:
        doc.save(str(target_path))
    doc.close()

    expected_full_text = "\n\n".join(expected_chapters.values())
    return GeneratedPdfSpec(
        path=target_path,
        expected_chapters=expected_chapters,
        expected_full_text=expected_full_text,
        header_text=header_text,
    )


def create_scanned_image_pdf(target_path: Path, page_count: int = 4) -> Path:
    """Metin katmanı olmayan, yalnızca görsel içeren (taranmış) PDF üretir."""
    doc = fitz.open()
    pix = fitz.Pixmap(fitz.csRGB, fitz.IRect(0, 0, 200, 200), False)
    pix.clear_with(240)
    img_bytes = pix.tobytes("png")

    for _ in range(page_count):
        page = doc.new_page(width=595, height=842)
        page.insert_image(fitz.Rect(50, 50, 545, 792), stream=img_bytes)

    target_path.parent.mkdir(parents=True, exist_ok=True)
    doc.save(str(target_path))
    doc.close()
    return target_path
