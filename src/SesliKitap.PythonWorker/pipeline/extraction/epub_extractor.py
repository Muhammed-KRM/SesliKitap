from __future__ import annotations

import re
import warnings
import zipfile
from pathlib import Path

import ebooklib
from bs4 import BeautifulSoup, XMLParsedAsHTMLWarning
from ebooklib import epub

from models.book_models import (
    BookMeta,
    ExtractedDocument,
    ExtractedPage,
    HtmlParagraph,
    HtmlSection,
    TextBlock,
    TextLine,
    TextSpan,
    TocEntry,
)
from models.events import ProcessingError
from pipeline.extraction.base_extractor import BaseExtractor, ProgressCallback
from utils.logger import get_logger

warnings.filterwarnings("ignore", category=XMLParsedAsHTMLWarning)
logger = get_logger(__name__)
_YEAR_RE = re.compile(r"\b(1[5-9]\d{2}|20\d{2})\b")
_MAX_UNCOMPRESSED_BYTES = 600 * 1024 * 1024  # 600 MB zip-bomb sınırı
_MAX_ZIP_FILES = 10_000


class EpubExtractor(BaseExtractor):
    """ebooklib + BeautifulSoup(lxml) tabanlı EPUB yapısal metin, TOC ve kapak çıkarıcı."""

    supported_format = "epub"

    def extract(self, path: Path, progress: ProgressCallback | None = None) -> ExtractedDocument:
        if not path.is_file():
            raise ProcessingError("FILE_NOT_FOUND", f"EPUB dosyası bulunamadı: {path}")

        self._validate_zip_safety(path)

        try:
            book = epub.read_epub(str(path), options={"ignore_ncx": False})
        except Exception as exc:
            raise ProcessingError("EPUB_OPEN_FAILED", f"EPUB dosyası okunamadı veya bozuk: {exc}") from exc

        toc_entries = self._extract_toc(book)
        toc_title_by_href = {
            self._normalize_href(entry.href): entry.title
            for entry in toc_entries
            if entry.href and entry.title
        }

        html_sections: list[HtmlSection] = []
        pages: list[ExtractedPage] = []

        spine_ids = [item_id for item_id, _ in book.spine]
        items_by_id = {item.get_id(): item for item in book.get_items()}
        doc_items = [
            items_by_id[i]
            for i in spine_ids
            if i in items_by_id and items_by_id[i].get_type() == ebooklib.ITEM_DOCUMENT
        ]
        if not doc_items:
            doc_items = list(book.get_items_of_type(ebooklib.ITEM_DOCUMENT))

        total_items = len(doc_items)
        for idx, item in enumerate(doc_items):
            href = self._normalize_href(item.get_name())
            raw_html = item.get_content()
            section_title, paragraphs = self._parse_html_document(raw_html, toc_title_by_href.get(href))

            if paragraphs:
                html_sections.append(HtmlSection(href=href, title=section_title, paragraphs=paragraphs))
                page_blocks = self._paragraphs_to_blocks(paragraphs, idx)
                pages.append(ExtractedPage(index=idx, width=600.0, height=800.0, blocks=page_blocks))

            if progress is not None and total_items > 0:
                pct = int(((idx + 1) / total_items) * 100)
                progress(pct, f"EPUB bölümleri okunuyor ({idx + 1}/{total_items})")

        if not html_sections:
            raise ProcessingError("EPUB_EMPTY", "EPUB dosyasında okunabilir metin bölümü bulunamadı.")

        # TOC girişlerine sayfa/bölüm indeksi eşle
        href_to_section_idx = {sec.href: i for i, sec in enumerate(html_sections)}
        for entry in toc_entries:
            norm = self._normalize_href(entry.href)
            if norm in href_to_section_idx:
                entry.page_index = href_to_section_idx[norm]

        meta = self._extract_meta(book, len(html_sections))
        cover_bytes = self._extract_cover(book)

        return ExtractedDocument(
            format="epub",
            pages=pages,
            toc=toc_entries,
            meta=meta,
            html_sections=html_sections,
            cover_bytes=cover_bytes,
        )

    @staticmethod
    def _validate_zip_safety(path: Path) -> None:
        try:
            with zipfile.ZipFile(path, "r") as zf:
                infos = zf.infolist()
                if len(infos) > _MAX_ZIP_FILES:
                    raise ProcessingError("EPUB_TOO_MANY_FILES", "EPUB arşivi çok fazla dosya içeriyor.")
                total_size = 0
                for info in infos:
                    if ".." in Path(info.filename).parts or info.filename.startswith(("/", "\\")):
                        raise ProcessingError("EPUB_INVALID_PATH", "EPUB arşivinde geçersiz dosya yolu tespit edildi.")
                    total_size += info.file_size
                    if total_size > _MAX_UNCOMPRESSED_BYTES:
                        raise ProcessingError("EPUB_ZIP_BOMB", "EPUB sıkıştırılmamış boyutu güvenlik sınırını aşıyor.")
        except ProcessingError:
            raise
        except Exception as exc:
            raise ProcessingError("EPUB_CORRUPT_ARCHIVE", f"Geçersiz EPUB/ZIP arşivi: {exc}") from exc

    def _parse_html_document(
        self,
        raw_html: bytes,
        toc_title: str | None,
    ) -> tuple[str | None, list[HtmlParagraph]]:
        soup = BeautifulSoup(raw_html, "lxml")

        # Gürültülü/dipnot etiketlerini kaldır
        for tag in list(soup.find_all(["script", "style", "nav", "aside", "sup", "a"])):
            if getattr(tag, "decomposed", False) or tag.attrs is None:
                continue
            epub_type = str(tag.get("epub:type") or "").lower()
            cls_val = tag.get("class")
            cls_list = " ".join(cls_val).lower() if isinstance(cls_val, list) else str(cls_val or "").lower()
            is_noise = (
                tag.name in ("script", "style", "nav")
                or "footnote" in epub_type
                or "noteref" in epub_type
                or "footnote" in cls_list
                or (tag.name == "sup" and tag.get_text(strip=True).lstrip("[(").rstrip("])").isdigit())
            )
            if is_noise:
                tag.decompose()

        for br in soup.find_all("br"):
            br.replace_with("\n")

        detected_title = toc_title
        paragraphs: list[HtmlParagraph] = []

        body = soup.body or soup
        block_tags = body.find_all(["h1", "h2", "h3", "h4", "h5", "h6", "p", "blockquote", "li"])

        if not block_tags:
            text = body.get_text(separator="\n", strip=True)
            for line in text.splitlines():
                cleaned = line.strip()
                if cleaned:
                    paragraphs.append(HtmlParagraph(text=cleaned, tag="p"))
            return detected_title, paragraphs

        for elem in block_tags:
            # İç içe blok etiketlerde mükerrer okumayı önle
            if elem.name in ("p", "blockquote", "li") and elem.find(["p", "h1", "h2", "h3"]):
                continue
            txt = re.sub(r"[ \t]+", " ", elem.get_text(separator=" ", strip=True)).strip()
            if not txt:
                continue

            is_heading = elem.name in ("h1", "h2", "h3", "h4", "h5", "h6")
            h_level = int(elem.name[1]) if is_heading else None
            if is_heading and detected_title is None:
                detected_title = txt

            paragraphs.append(
                HtmlParagraph(
                    text=txt,
                    tag=elem.name,
                    is_heading=is_heading,
                    heading_level=h_level,
                )
            )

        return detected_title, paragraphs

    @staticmethod
    def _paragraphs_to_blocks(paragraphs: list[HtmlParagraph], page_idx: int) -> list[TextBlock]:
        blocks: list[TextBlock] = []
        y = 50.0
        for p in paragraphs:
            font_size = 16.0 if p.is_heading else 11.0
            flags = 16 if p.is_heading else 0
            span = TextSpan(text=p.text, size=font_size, flags=flags, bbox=(50.0, y, 550.0, y + 18.0))
            line = TextLine(spans=[span], bbox=(50.0, y, 550.0, y + 18.0))
            blocks.append(
                TextBlock(
                    lines=[line],
                    bbox=(50.0, y, 550.0, y + 18.0),
                    page_index=page_idx,
                    kind="heading" if p.is_heading else "body",
                )
            )
            y += 24.0
        return blocks

    def _extract_toc(self, book: epub.EpubBook) -> list[TocEntry]:
        entries: list[TocEntry] = []

        def _walk(nodes: list | tuple, level: int) -> None:
            for node in nodes:
                if isinstance(node, epub.Link):
                    title = (node.title or "").strip()
                    if title:
                        entries.append(TocEntry(level=level, title=title, href=node.href))
                elif isinstance(node, tuple) and len(node) == 2:
                    section, children = node
                    title = getattr(section, "title", None)
                    href = getattr(section, "href", None)
                    if title and str(title).strip():
                        entries.append(TocEntry(level=level, title=str(title).strip(), href=href))
                    if isinstance(children, (list, tuple)):
                        _walk(children, level + 1)

        try:
            if book.toc:
                _walk(book.toc, 1)
        except Exception as exc:
            logger.warning("EPUB TOC ayrıştırılamadı: %s", exc)
        return entries

    @staticmethod
    def _normalize_href(href: str | None) -> str:
        if not href:
            return ""
        base = href.split("#", 1)[0].strip().lstrip("./")
        return base

    @staticmethod
    def _extract_meta(book: epub.EpubBook, section_count: int) -> BookMeta:
        def _first_dc(field_name: str) -> str | None:
            vals = book.get_metadata("DC", field_name)
            if vals and vals[0] and vals[0][0]:
                return str(vals[0][0]).strip()
            return None

        title = _first_dc("title")
        author = _first_dc("creator")
        lang = (_first_dc("language") or "tr").split("-")[0].lower()
        date_str = _first_dc("date") or ""
        year: int | None = None
        m = _YEAR_RE.search(date_str)
        if m:
            year = int(m.group(1))

        return BookMeta(
            title=title,
            author=author,
            language=lang or "tr",
            year=year,
            page_count=section_count,
        )

    @staticmethod
    def _extract_cover(book: epub.EpubBook) -> bytes | None:
        try:
            cover_items = list(book.get_items_of_type(ebooklib.ITEM_COVER))
            if cover_items:
                return cover_items[0].get_content()

            for item in book.get_items_of_type(ebooklib.ITEM_IMAGE):
                name_lower = (item.get_name() or "").lower()
                if "cover" in name_lower or "kapak" in name_lower:
                    return item.get_content()
        except Exception as exc:
            logger.warning("EPUB kapak görüntüsü çıkarılamadı: %s", exc)
        return None
