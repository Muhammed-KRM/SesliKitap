from __future__ import annotations

import re

from models.book_models import CleaningStats, ExtractedDocument, TextBlock, TextLine, TextSpan

_FOOTNOTE_START_RE = re.compile(r"^(\d{1,3}[.)]?|[*†‡§¹²³⁴⁵⁶⁷⁸⁹⁰]+)\s*\S+")
_BRACKET_REF_RE = re.compile(r"\[\d{1,3}\]")
_UNICODE_SUPERSCRIPT_RE = re.compile(r"[¹²³⁴⁵⁶⁷⁸⁹⁰]+")
_SUPERSCRIPT_ONLY_RE = re.compile(r"^[\d*†‡§¹²³⁴⁵⁶⁷⁸⁹⁰()\[\]]+$")


class FootnoteRemover:
    """Sayfa altı dipnot bloklarını ve metin içi dipnot referans işaretlerini temizler."""

    def __init__(self, bottom_region_ratio: float = 0.35, size_ratio: float = 0.85) -> None:
        self.bottom_region_ratio = bottom_region_ratio
        self.size_ratio = size_ratio

    def remove(self, doc: ExtractedDocument, stats: CleaningStats) -> float:
        body_font_size = self.estimate_body_font_size(doc)

        for page in doc.pages:
            bottom_threshold_y = page.height * (1.0 - self.bottom_region_ratio)
            kept_blocks: list[TextBlock] = []

            for block in page.blocks:
                if doc.format == "pdf" and self._is_footnote_block(block, bottom_threshold_y, body_font_size):
                    stats.removed_footnotes += 1
                    stats.add_removed_sample(block.text[:120])
                    continue

                cleaned_lines = self._clean_inline_markers(block.lines, body_font_size, stats)
                if cleaned_lines:
                    block.lines = cleaned_lines
                    kept_blocks.append(block)

            page.blocks = kept_blocks

        return body_font_size

    @staticmethod
    def estimate_body_font_size(doc: ExtractedDocument) -> float:
        """Tüm span'ların karakter ağırlıklı medyan yazı tipi boyutunu hesaplar."""
        weighted: list[tuple[float, int]] = []
        for page in doc.pages:
            for block in page.blocks:
                for line in block.lines:
                    for span in line.spans:
                        char_len = len(span.text.strip())
                        if char_len > 0 and span.size > 0:
                            weighted.append((round(span.size, 1), char_len))

        if not weighted:
            return 11.0

        weighted.sort(key=lambda item: item[0])
        total_chars = sum(w for _, w in weighted)
        half = total_chars / 2.0
        cumulative = 0
        for size, weight in weighted:
            cumulative += weight
            if cumulative >= half:
                return size
        return weighted[-1][0]

    def _is_footnote_block(self, block: TextBlock, bottom_threshold_y: float, body_font_size: float) -> bool:
        if not block.lines:
            return False

        _, y0, _, _ = block.bbox
        if y0 < bottom_threshold_y:
            return False

        total_chars = 0
        weighted_size = 0.0
        for line in block.lines:
            for span in line.spans:
                c = len(span.text.strip())
                total_chars += c
                weighted_size += span.size * c

        if total_chars == 0:
            return False

        avg_size = weighted_size / total_chars
        if avg_size >= body_font_size * self.size_ratio:
            return False

        first_line_text = block.lines[0].text.strip()
        return _FOOTNOTE_START_RE.match(first_line_text) is not None

    @staticmethod
    def _clean_inline_markers(
        lines: list[TextLine],
        body_font_size: float,
        stats: CleaningStats,
    ) -> list[TextLine]:
        cleaned_lines: list[TextLine] = []
        for line in lines:
            kept_spans: list[TextSpan] = []
            for span in line.spans:
                stripped = span.text.strip()
                if not stripped:
                    kept_spans.append(span)
                    continue

                is_small_super = span.is_superscript or (span.size < body_font_size * 0.78)
                if is_small_super and _SUPERSCRIPT_ONLY_RE.match(stripped):
                    stats.removed_footnotes += 1
                    continue

                new_text, n_brackets = _BRACKET_REF_RE.subn("", span.text)
                new_text, n_supers = _UNICODE_SUPERSCRIPT_RE.subn("", new_text)
                if n_brackets + n_supers > 0:
                    stats.removed_footnotes += n_brackets + n_supers
                    span.text = new_text

                if span.text:
                    kept_spans.append(span)

            if kept_spans and "".join(s.text for s in kept_spans).strip():
                line.spans = kept_spans
                cleaned_lines.append(line)

        return cleaned_lines
