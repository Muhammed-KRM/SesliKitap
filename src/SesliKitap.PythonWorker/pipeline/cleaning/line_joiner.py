from __future__ import annotations

import re

from models.book_models import (
    CleaningStats,
    ExtractedDocument,
    ParagraphWithStyle,
    TextBlock,
    TextLine,
)
from utils.turkish import TURKISH_LOWER, TURKISH_UPPER

_LOWER_CHARS = set(TURKISH_LOWER)
_UPPER_CHARS = set(TURKISH_UPPER)
_TERMINAL_PUNCT = (".", "!", "?", ":", "…", '"', "”", "'", "’", "»")
_LIGATURE_MAP = str.maketrans(
    {
        "\u00ad": "",  # soft hyphen
        "ﬁ": "fi",
        "ﬂ": "fl",
        "ﬀ": "ff",
        "ﬃ": "ffi",
        "ﬄ": "ffl",
        "“": '"',
        "”": '"',
        "„": '"',
        "‘": "'",
        "’": "'",
    }
)
_MULTI_SPACE_RE = re.compile(r"[ \t]+")


class LineJoiner:
    """Satırları paragraflara dönüştürür, Türkçe kurallarına göre tireleri ve sayfa geçişlerini birleştirir."""

    def join_document(
        self,
        doc: ExtractedDocument,
        body_font_size: float,
        stats: CleaningStats,
    ) -> list[list[ParagraphWithStyle]]:
        paragraphs_by_page: list[list[ParagraphWithStyle]] = []

        for page in doc.pages:
            page_paragraphs: list[ParagraphWithStyle] = []
            for block in page.blocks:
                block_pars = self._block_to_paragraphs(block, body_font_size, stats)
                page_paragraphs.extend(block_pars)
            self._merge_within_page(page_paragraphs, body_font_size, stats)
            paragraphs_by_page.append(page_paragraphs)

        self._merge_across_pages(paragraphs_by_page, body_font_size, stats)
        return paragraphs_by_page

    def _block_to_paragraphs(
        self,
        block: TextBlock,
        body_font_size: float,
        stats: CleaningStats,
    ) -> list[ParagraphWithStyle]:
        if not block.lines:
            return []

        if block.kind == "heading":
            joined = " ".join(ln.text.strip() for ln in block.lines if ln.text.strip())
            norm = self.normalize_text(joined)
            if not norm:
                return []
            return [
                ParagraphWithStyle(
                    text=norm,
                    page_index=block.page_index,
                    font_size=block.lines[0].avg_size,
                    is_bold=block.lines[0].is_bold,
                    is_heading_tag=True,
                )
            ]

        groups: list[list[TextLine]] = []
        current_group: list[TextLine] = []
        block_x0 = block.bbox[0]
        block_width = max(1.0, block.bbox[2] - block.bbox[0])

        for line in block.lines:
            txt = line.text.strip()
            if not txt:
                continue

            if not current_group:
                current_group.append(line)
                continue

            prev_line = current_group[-1]
            if self._is_new_paragraph(prev_line, line, block_x0, block_width, body_font_size):
                groups.append(current_group)
                current_group = [line]
            else:
                current_group.append(line)

        if current_group:
            groups.append(current_group)

        result: list[ParagraphWithStyle] = []
        for grp in groups:
            par_text = self._join_lines(grp, stats)
            par_text = self.normalize_text(par_text)
            if not par_text:
                continue
            avg_font = sum(ln.avg_size for ln in grp) / len(grp)
            is_bold = all(ln.is_bold for ln in grp)
            result.append(
                ParagraphWithStyle(
                    text=par_text,
                    page_index=block.page_index,
                    font_size=round(avg_font, 2),
                    is_bold=is_bold,
                    is_heading_tag=False,
                )
            )

        return result

    @staticmethod
    def _is_new_paragraph(
        prev_line: TextLine,
        curr_line: TextLine,
        block_x0: float,
        block_width: float,
        body_font_size: float,
    ) -> bool:
        prev_x0, prev_y0, prev_x1, prev_y1 = prev_line.bbox
        curr_x0, curr_y0, _, _ = curr_line.bbox

        if curr_line.text.lstrip().startswith(("—", "–", "- ")):
            return True

        line_height = max(8.0, prev_y1 - prev_y0)
        vertical_gap = curr_y0 - prev_y1
        if vertical_gap > line_height * 1.5:
            return True

        avg_char_width = max(4.0, body_font_size * 0.5)
        if curr_x0 > block_x0 + 1.5 * avg_char_width and curr_x0 > prev_x0 + avg_char_width:
            return True

        prev_width = prev_x1 - prev_x0
        prev_txt = prev_line.text.rstrip()
        return bool(prev_width < block_width * 0.60 and prev_txt.endswith(_TERMINAL_PUNCT))

    def _join_lines(self, lines: list[TextLine], stats: CleaningStats) -> str:
        if not lines:
            return ""

        acc = lines[0].text.strip()
        for next_line in lines[1:]:
            nxt = next_line.text.strip()
            if not nxt:
                continue
            acc = self._join_two_strings(acc, nxt, stats)
        return acc

    @staticmethod
    def _join_two_strings(left: str, right: str, stats: CleaningStats) -> str:
        left_clean = left.rstrip().replace("\u00ad", "")
        right_clean = right.lstrip().replace("\u00ad", "")
        if not left_clean:
            return right_clean
        if not right_clean:
            return left_clean

        if left_clean.endswith("-") and len(left_clean) >= 2:
            char_before_hyphen = left_clean[-2]
            first_next_char = right_clean[0]

            if char_before_hyphen in _LOWER_CHARS and first_next_char in _LOWER_CHARS:
                stats.joined_hyphens += 1
                return left_clean[:-1] + right_clean

            if first_next_char in _UPPER_CHARS:
                stats.joined_hyphens += 1
                return left_clean + right_clean

        return f"{left_clean} {right_clean}"

    def _merge_within_page(
        self,
        page_paragraphs: list[ParagraphWithStyle],
        body_font_size: float,
        stats: CleaningStats,
    ) -> None:
        idx = 0
        while idx < len(page_paragraphs) - 1:
            curr_par = page_paragraphs[idx]
            next_par = page_paragraphs[idx + 1]
            if (
                not curr_par.is_heading_tag
                and not next_par.is_heading_tag
                and curr_par.font_size < body_font_size * 1.25
                and next_par.font_size < body_font_size * 1.25
            ):
                curr_txt = curr_par.text.rstrip()
                next_txt = next_par.text.lstrip()
                if curr_txt.endswith("-") and next_txt and next_txt[0] in _LOWER_CHARS:
                    merged = self._join_two_strings(curr_txt, next_txt, stats)
                    curr_par.text = self.normalize_text(merged)
                    page_paragraphs.pop(idx + 1)
                    continue
            idx += 1

    def _merge_across_pages(
        self,
        paragraphs_by_page: list[list[ParagraphWithStyle]],
        body_font_size: float,
        stats: CleaningStats,
    ) -> None:
        for i in range(len(paragraphs_by_page) - 1):
            curr_page = paragraphs_by_page[i]
            next_page = paragraphs_by_page[i + 1]
            if not curr_page or not next_page:
                continue

            last_par = curr_page[-1]
            first_next = next_page[0]

            if last_par.is_heading_tag or first_next.is_heading_tag:
                continue
            if last_par.font_size >= body_font_size * 1.25 or first_next.font_size >= body_font_size * 1.25:
                continue

            last_txt = last_par.text.rstrip()
            next_txt = first_next.text.lstrip()
            if not last_txt or not next_txt:
                continue

            if not last_txt.endswith(_TERMINAL_PUNCT) and next_txt[0] in _LOWER_CHARS:
                merged = self._join_two_strings(last_txt, next_txt, stats)
                last_par.text = self.normalize_text(merged)
                next_page.pop(0)

    @staticmethod
    def normalize_text(text: str) -> str:
        if not text:
            return ""
        translated = text.translate(_LIGATURE_MAP)
        return _MULTI_SPACE_RE.sub(" ", translated).strip()
