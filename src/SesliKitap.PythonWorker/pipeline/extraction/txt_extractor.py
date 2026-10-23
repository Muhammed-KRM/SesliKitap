from __future__ import annotations

import re
from pathlib import Path

from charset_normalizer import from_bytes

from models.book_models import (
    BookMeta,
    ExtractedDocument,
    ExtractedPage,
    TextBlock,
    TextLine,
    TextSpan,
)
from models.events import ProcessingError
from pipeline.extraction.base_extractor import BaseExtractor, ProgressCallback

_TURKISH_SPECIFIC_CHARS = set("ğĞşŞıİçÇöÖüÜ")


class TxtExtractor(BaseExtractor):
    """Türkçe karakter kodlama duyarlı (UTF-8, UTF-16, Windows-1254, ISO-8859-9) düz metin çıkarıcı."""

    supported_format = "txt"

    def extract(self, path: Path, progress: ProgressCallback | None = None) -> ExtractedDocument:
        if not path.is_file():
            raise ProcessingError("FILE_NOT_FOUND", f"TXT dosyası bulunamadı: {path}")

        raw_bytes = path.read_bytes()
        if not raw_bytes.strip():
            raise ProcessingError("TXT_EMPTY", "Yüklenen metin dosyası boş.")

        text = self._decode_turkish_aware(raw_bytes)
        text = text.replace("\r\n", "\n").replace("\r", "\n")

        raw_paragraphs = re.split(r"\n\s*\n", text)
        blocks: list[TextBlock] = []
        y = 50.0

        for raw_par in raw_paragraphs:
            lines_in_par = [ln.strip() for ln in raw_par.split("\n") if ln.strip()]
            if not lines_in_par:
                continue

            text_lines: list[TextLine] = []
            for ln in lines_in_par:
                span = TextSpan(text=ln, size=11.0, flags=0, bbox=(50.0, y, 550.0, y + 14.0))
                text_lines.append(TextLine(spans=[span], bbox=(50.0, y, 550.0, y + 14.0)))
                y += 16.0

            blocks.append(
                TextBlock(
                    lines=text_lines,
                    bbox=(50.0, y - len(text_lines) * 16.0, 550.0, y),
                    page_index=0,
                    kind="body",
                )
            )
            y += 12.0

        if not blocks:
            raise ProcessingError("TXT_EMPTY", "Yüklenen metin dosyasında okunabilir metin bulunamadı.")

        if progress is not None:
            progress(100, "Metin dosyası okundu.")

        page = ExtractedPage(index=0, width=600.0, height=max(800.0, y), blocks=blocks)
        meta = BookMeta(title=None, author=None, language="tr", page_count=1)
        return ExtractedDocument(format="txt", pages=[page], toc=[], meta=meta)

    @staticmethod
    def _decode_turkish_aware(raw_bytes: bytes) -> str:
        # 1) BOM kontrolleri
        if raw_bytes.startswith(b"\xef\xbb\xbf"):
            return raw_bytes.decode("utf-8-sig")
        if raw_bytes.startswith((b"\xff\xfe", b"\xfe\xff")):
            return raw_bytes.decode("utf-16")

        # 2) Kesin UTF-8 geçerli mi?
        try:
            return raw_bytes.decode("utf-8", errors="strict")
        except UnicodeDecodeError:
            pass

        # 3) Türkçe 8-bit kodlamalar (cp1254 vs charset_normalizer) karşılaştırması
        candidates: list[tuple[int, str]] = []
        for enc in ("cp1254", "iso-8859-9"):
            try:
                decoded = raw_bytes.decode(enc, errors="strict")
                tr_score = sum(1 for ch in decoded if ch in _TURKISH_SPECIFIC_CHARS)
                candidates.append((tr_score, decoded))
            except UnicodeDecodeError:
                continue

        best_match = from_bytes(raw_bytes).best()
        if best_match is not None:
            cn_str = str(best_match)
            tr_score = sum(1 for ch in cn_str if ch in _TURKISH_SPECIFIC_CHARS)
            candidates.append((tr_score, cn_str))

        if candidates:
            candidates.sort(key=lambda item: item[0], reverse=True)
            return candidates[0][1]

        return raw_bytes.decode("utf-8", errors="replace")
