from __future__ import annotations

import math
import re
from collections import Counter

from rapidfuzz import fuzz

from models.book_models import CleaningStats, ExtractedDocument, ExtractedPage, TextBlock, TextLine
from utils.turkish import tr_lower

_PAGE_NUM_PATTERNS = [
    re.compile(r"^\d{1,4}$"),
    re.compile(r"^[-–—~•*]\s*\d{1,4}\s*[-–—~•*]?$"),
    re.compile(r"^(?:sayfa|s\.|p\.|pg\.)\s*\d{1,4}$", re.IGNORECASE),
    re.compile(r"^\d{1,4}\s*/\s*\d{1,4}$"),
    re.compile(r"^[ivxlcdm]{1,7}$", re.IGNORECASE),
]
_DIGIT_RE = re.compile(r"\d+")
_SPACE_RE = re.compile(r"\s+")


class HeaderFooterRemover:
    """Sayfalarda tekrar eden üst/alt bilgileri ve tekil sayfa numaralarını tespit edip temizler."""

    def __init__(self, region_ratio: float = 0.08, repeat_threshold: float = 0.30) -> None:
        self.region_ratio = region_ratio
        self.repeat_threshold = repeat_threshold

    def remove(self, doc: ExtractedDocument, stats: CleaningStats) -> None:
        pages = doc.pages
        total_pages = len(pages)
        if total_pages == 0:
            return

        # 1) Aday satırları topla
        all_page_norms: dict[int, set[str]] = {}
        odd_page_norms: dict[int, set[str]] = {}
        even_page_norms: dict[int, set[str]] = {}

        for page in pages:
            page_candidates: set[str] = set()
            for block in page.blocks:
                for line in block.lines:
                    if self._is_in_margin(line, page):
                        txt = line.text.strip()
                        if not txt or self._is_page_number(txt):
                            continue
                        norm = self._normalize_candidate(txt)
                        if norm and len(norm) <= 120:
                            page_candidates.add(norm)

            all_page_norms[page.index] = page_candidates
            if page.index % 2 == 1:
                odd_page_norms[page.index] = page_candidates
            else:
                even_page_norms[page.index] = page_candidates

        repeating_patterns = self._find_repeating_patterns(
            all_page_norms,
            odd_page_norms,
            even_page_norms,
            total_pages,
        )

        # 2) İşaretlenen üst/alt bilgileri ve sayfa numaralarını sil
        for page in pages:
            kept_blocks: list[TextBlock] = []
            for block in page.blocks:
                kept_lines: list[TextLine] = []
                for line in block.lines:
                    txt = line.text.strip()
                    if not txt:
                        continue

                    if self._is_in_margin(line, page):
                        if self._is_page_number(txt):
                            stats.removed_page_numbers += 1
                            stats.add_removed_sample(txt)
                            continue

                        norm = self._normalize_candidate(txt)
                        if self._matches_repeating(norm, repeating_patterns):
                            stats.removed_headers_footers += 1
                            stats.add_removed_sample(txt)
                            continue

                    kept_lines.append(line)

                if kept_lines:
                    block.lines = kept_lines
                    kept_blocks.append(block)

            page.blocks = kept_blocks

    def _is_in_margin(self, line: TextLine, page: ExtractedPage) -> bool:
        _, y0, _, y1 = line.bbox
        top_limit = page.height * self.region_ratio
        bottom_limit = page.height * (1.0 - self.region_ratio)
        return y1 <= top_limit or y0 >= bottom_limit

    @staticmethod
    def _is_page_number(text: str) -> bool:
        cleaned = text.strip()
        return any(pat.match(cleaned) is not None for pat in _PAGE_NUM_PATTERNS)

    @staticmethod
    def _normalize_candidate(text: str) -> str:
        lowered = tr_lower(text.strip())
        no_digits = _DIGIT_RE.sub("#", lowered)
        return _SPACE_RE.sub(" ", no_digits).strip(" -–—|•.")

    def _find_repeating_patterns(
        self,
        all_pages: dict[int, set[str]],
        odd_pages: dict[int, set[str]],
        even_pages: dict[int, set[str]],
        total_pages: int,
    ) -> set[str]:
        if total_pages < 3:
            return set()

        repeating: set[str] = set()

        def _collect_from_group(group: dict[int, set[str]]) -> None:
            group_size = len(group)
            if group_size < 2:
                return
            min_hits = max(2, math.ceil(group_size * self.repeat_threshold))
            counter: Counter[str] = Counter()
            for norms in group.values():
                for n in norms:
                    counter[n] += 1
            for norm_str, count in counter.items():
                if count >= min_hits:
                    repeating.add(norm_str)

        _collect_from_group(all_pages)
        _collect_from_group(odd_pages)
        _collect_from_group(even_pages)
        return repeating

    @staticmethod
    def _matches_repeating(candidate_norm: str, repeating_patterns: set[str]) -> bool:
        if not candidate_norm or not repeating_patterns:
            return False
        if candidate_norm in repeating_patterns:
            return True
        return any(
            len(candidate_norm) >= 5 and fuzz.ratio(candidate_norm, pat) >= 90.0
            for pat in repeating_patterns
        )
