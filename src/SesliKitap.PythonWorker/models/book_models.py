from __future__ import annotations

from dataclasses import dataclass, field


@dataclass(slots=True)
class TextSpan:
    """Sayfa içindeki tek bir metin parçası ve yazı tipi özellikleri."""

    text: str
    size: float
    flags: int
    bbox: tuple[float, float, float, float]

    @property
    def is_superscript(self) -> bool:
        return bool(self.flags & 1)

    @property
    def is_bold(self) -> bool:
        return bool(self.flags & 16)


@dataclass(slots=True)
class TextLine:
    """Aynı yatay satırdaki metin parçaları dizisi."""

    spans: list[TextSpan]
    bbox: tuple[float, float, float, float]

    @property
    def text(self) -> str:
        return "".join(s.text for s in self.spans)

    @property
    def avg_size(self) -> float:
        total_chars = sum(len(s.text.strip()) for s in self.spans)
        if total_chars == 0:
            return self.spans[0].size if self.spans else 11.0
        return sum(s.size * len(s.text.strip()) for s in self.spans) / total_chars

    @property
    def is_bold(self) -> bool:
        non_empty = [s for s in self.spans if s.text.strip()]
        if not non_empty:
            return False
        bold_chars = sum(len(s.text.strip()) for s in non_empty if s.is_bold)
        total_chars = sum(len(s.text.strip()) for s in non_empty)
        return total_chars > 0 and (bold_chars / total_chars) >= 0.7


@dataclass(slots=True)
class TextBlock:
    """Sayfa içindeki metin bloğu."""

    lines: list[TextLine]
    bbox: tuple[float, float, float, float]
    page_index: int
    kind: str = "body"  # body | header | footer | footnote | heading

    @property
    def text(self) -> str:
        return "\n".join(line.text for line in self.lines if line.text.strip())


@dataclass(slots=True)
class ExtractedPage:
    """Kaynak belgeden çıkarılan tek bir sayfa."""

    index: int
    width: float
    height: float
    blocks: list[TextBlock]
    has_images: bool = False

    @property
    def char_count(self) -> int:
        return sum(len(line.text.strip()) for block in self.blocks for line in block.lines)


@dataclass(slots=True)
class TocEntry:
    """İçindekiler (TOC / Outline) girişi."""

    level: int
    title: str
    page_index: int | None = None
    href: str | None = None


@dataclass(slots=True)
class BookMeta:
    """Belge üst verisi."""

    title: str | None = None
    author: str | None = None
    language: str | None = "tr"
    year: int | None = None
    page_count: int | None = None


@dataclass(slots=True)
class HtmlParagraph:
    """EPUB HTML belgesinden çıkarılan yapısal paragraf veya başlık."""

    text: str
    tag: str = "p"
    is_heading: bool = False
    heading_level: int | None = None


@dataclass(slots=True)
class HtmlSection:
    """EPUB spine öğesine karşılık gelen HTML bölümü."""

    href: str
    title: str | None
    paragraphs: list[HtmlParagraph]


@dataclass
class ExtractedDocument:
    """Kaynak dosyadan çıkarılan ham yapısal belge."""

    format: str
    pages: list[ExtractedPage]
    toc: list[TocEntry] = field(default_factory=list)
    meta: BookMeta = field(default_factory=BookMeta)
    html_sections: list[HtmlSection] | None = None
    cover_bytes: bytes | None = None


@dataclass(slots=True)
class ParagraphWithStyle:
    """Temizlenmiş ve stil ipuçları korunmuş paragraf."""

    text: str
    page_index: int
    font_size: float = 11.0
    is_bold: bool = False
    is_heading_tag: bool = False


@dataclass
class CleaningStats:
    """Temizlik adımları sırasında toplanan sayaçlar ve örnekler."""

    removed_headers_footers: int = 0
    removed_page_numbers: int = 0
    removed_footnotes: int = 0
    joined_hyphens: int = 0
    sample_removed_lines: list[str] = field(default_factory=list)

    def add_removed_sample(self, line: str) -> None:
        cleaned = line.strip()
        if cleaned and len(self.sample_removed_lines) < 20:
            self.sample_removed_lines.append(cleaned)

    @property
    def deleted_headers(self) -> int:
        return self.removed_headers_footers

    @property
    def deleted_page_numbers(self) -> int:
        return self.removed_page_numbers

    @property
    def deleted_footnotes(self) -> int:
        return self.removed_footnotes

    @property
    def merged_hyphens(self) -> int:
        return self.joined_hyphens

    @property
    def sample_deleted_lines(self) -> list[str]:
        return self.sample_removed_lines


@dataclass
class CleanedDocument:
    """Temizlenmiş belge çıktısı."""

    paragraphs_by_page: list[list[ParagraphWithStyle]]
    body_font_size: float
    stats: CleaningStats = field(default_factory=CleaningStats)

    @property
    def all_paragraphs(self) -> list[ParagraphWithStyle]:
        return [p for page_pars in self.paragraphs_by_page for p in page_pars]


@dataclass
class ChapterDraft:
    """Tespit edilen bölüm taslağı."""

    title: str
    paragraphs: list[str]
    included: bool = True
    exclusion_reason: str | None = None

    @property
    def clean_text(self) -> str:
        return "\n\n".join(p.strip() for p in self.paragraphs if p.strip())
