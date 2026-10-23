from __future__ import annotations

import re
from pathlib import Path

import fitz

from models.book_models import (
    BookMeta,
    ExtractedDocument,
    ExtractedPage,
    TextBlock,
    TextLine,
    TextSpan,
    TocEntry,
)
from models.events import ProcessingError
from pipeline.extraction.base_extractor import BaseExtractor, ProgressCallback
from utils.logger import get_logger

logger = get_logger(__name__)
_YEAR_RE = re.compile(r"\b(1[5-9]\d{2}|20\d{2})\b")


class PdfExtractor(BaseExtractor):
    """PyMuPDF (fitz) tabanlı PDF yapısal metin, outline ve kapak çıkarıcı."""

    supported_format = "pdf"

    def extract(self, path: Path, progress: ProgressCallback | None = None) -> ExtractedDocument:
        if not path.is_file():
            raise ProcessingError("FILE_NOT_FOUND", f"PDF dosyası bulunamadı: {path}")

        try:
            doc = fitz.open(str(path))
        except Exception as exc:
            raise ProcessingError("PDF_OPEN_FAILED", f"PDF dosyası açılamadı veya bozuk: {exc}") from exc

        try:
            if doc.is_encrypted:
                auth_result = doc.authenticate("")
                if not auth_result:
                    raise ProcessingError(
                        "PDF_ENCRYPTED",
                        "Bu PDF dosyası şifreli olduğu için okunamıyor. Lütfen şifresiz bir kopya yükleyin.",
                    )

            total_pages = len(doc)
            if total_pages == 0:
                raise ProcessingError("PDF_EMPTY", "PDF dosyası hiç sayfa içermiyor.")

            toc = self._extract_toc(doc, total_pages)
            meta = self._extract_meta(doc, total_pages)
            cover_bytes = self._render_cover(doc)

            pages: list[ExtractedPage] = []
            for page_idx in range(total_pages):
                page = doc[page_idx]
                extracted_page = self._extract_page(page, page_idx)
                pages.append(extracted_page)

                if progress is not None and (page_idx % 20 == 0 or page_idx == total_pages - 1):
                    pct = int(((page_idx + 1) / total_pages) * 100)
                    progress(pct, f"PDF sayfaları okunuyor ({page_idx + 1}/{total_pages})")

            return ExtractedDocument(
                format="pdf",
                pages=pages,
                toc=toc,
                meta=meta,
                cover_bytes=cover_bytes,
            )
        finally:
            doc.close()

    def _extract_page(self, page: fitz.Page, page_idx: int) -> ExtractedPage:
        rect = page.rect
        width = float(rect.width) if rect.width > 0 else 595.0
        height = float(rect.height) if rect.height > 0 else 842.0

        # TEXT_DEHYPHENATE bilerek kapalı: tire birleştirmeyi Türkçe kurallarla LineJoiner yapar
        flags = fitz.TEXT_PRESERVE_WHITESPACE | fitz.TEXT_PRESERVE_LIGATURES
        raw_dict = page.get_text("dict", flags=flags)
        raw_blocks = raw_dict.get("blocks", [])

        has_images = False
        text_blocks: list[TextBlock] = []

        for b in raw_blocks:
            b_type = b.get("type", 0)
            if b_type == 1:
                has_images = True
                continue
            if b_type != 0:
                continue

            b_bbox = tuple(float(v) for v in b.get("bbox", (0.0, 0.0, 0.0, 0.0)))
            lines: list[TextLine] = []
            for raw_line in b.get("lines", []):
                l_bbox = tuple(float(v) for v in raw_line.get("bbox", b_bbox))
                spans: list[TextSpan] = []
                for raw_span in raw_line.get("spans", []):
                    txt = raw_span.get("text", "")
                    if not txt:
                        continue
                    s_bbox = tuple(float(v) for v in raw_span.get("bbox", l_bbox))
                    spans.append(
                        TextSpan(
                            text=txt,
                            size=float(raw_span.get("size", 11.0)),
                            flags=int(raw_span.get("flags", 0)),
                            bbox=s_bbox,  # type: ignore[arg-type]
                        )
                    )
                if spans:
                    lines.append(TextLine(spans=spans, bbox=l_bbox))  # type: ignore[arg-type]

            if lines:
                text_blocks.append(
                    TextBlock(
                        lines=lines,
                        bbox=b_bbox,  # type: ignore[arg-type]
                        page_index=page_idx,
                    )
                )

        if not has_images and len(page.get_images(full=False)) > 0:
            has_images = True

        return ExtractedPage(
            index=page_idx,
            width=width,
            height=height,
            blocks=text_blocks,
            has_images=has_images,
        )

    @staticmethod
    def _extract_toc(doc: fitz.Document, total_pages: int) -> list[TocEntry]:
        entries: list[TocEntry] = []
        try:
            raw_toc = doc.get_toc(simple=True)
            for item in raw_toc:
                if len(item) < 3:
                    continue
                level, title, page_1based = int(item[0]), str(item[1]).strip(), int(item[2])
                if not title:
                    continue
                page_idx = max(0, min(total_pages - 1, page_1based - 1)) if page_1based >= 1 else 0
                entries.append(TocEntry(level=max(1, level), title=title, page_index=page_idx))
        except Exception as exc:
            logger.warning("PDF TOC okunamadı: %s", exc)
        return entries

    @staticmethod
    def _extract_meta(doc: fitz.Document, total_pages: int) -> BookMeta:
        md = doc.metadata or {}
        title = (md.get("title") or "").strip() or None
        author = (md.get("author") or "").strip() or None
        creation_date = md.get("creationDate") or md.get("modDate") or ""
        year: int | None = None
        m = _YEAR_RE.search(creation_date)
        if m:
            year = int(m.group(1))
        return BookMeta(
            title=title,
            author=author,
            language="tr",
            year=year,
            page_count=total_pages,
        )

    @staticmethod
    def _render_cover(doc: fitz.Document) -> bytes | None:
        try:
            first_page = doc[0]
            w = first_page.rect.width or 595.0
            scale = min(1.5, 600.0 / max(w, 1.0))
            matrix = fitz.Matrix(scale, scale)
            pix = first_page.get_pixmap(matrix=matrix, alpha=False)
            return pix.tobytes("jpeg", jpg_quality=85)
        except Exception as exc:
            logger.warning("PDF kapak görüntüsü üretilemedi: %s", exc)
            return None
