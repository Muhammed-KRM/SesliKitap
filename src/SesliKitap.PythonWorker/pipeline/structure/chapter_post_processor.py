from __future__ import annotations

import re
from pathlib import Path
from typing import Any

import yaml

from models.book_models import ChapterDraft
from utils.turkish import count_words, fold_turkish, tr_title_case, tr_upper

_KUNYE_KEYWORDS = ("isbn", "yayınevi", "yayinevi", "baskı", "baski", "copyright", "tüm hakları", "tum haklari")
_MULTISPACE_RE = re.compile(r"\s+")


class ChapterPostProcessor:
    """Tespit edilen bölüm taslaklarını temizler, kısa bölümleri birleştirir ve otomatik hariç tutma kurallarını uygular."""

    def __init__(self, min_chapter_words: int = 300, rules_path: Path | None = None) -> None:
        self.min_chapter_words = min_chapter_words
        default_path = Path(__file__).resolve().parents[2] / "resources" / "excluded_chapter_titles.yaml"
        self._rules = self._load_rules(rules_path or default_path)

    def process(self, drafts: list[ChapterDraft]) -> list[ChapterDraft]:
        # 1) Boş bölümleri at ve başlıkları temizle
        non_empty: list[ChapterDraft] = []
        for draft in drafts:
            pars = [p.strip() for p in draft.paragraphs if p.strip()]
            if not pars:
                continue
            draft.paragraphs = pars
            draft.title = self.clean_title(draft.title, fallback_index=len(non_empty) + 1)
            non_empty.append(draft)

        if not non_empty:
            return []

        # 2) Otomatik hariç tutma değerlendirmesi (birleştirmeden önce, böylece İçindekiler/Kaynakça ana metne karışmaz)
        total = len(non_empty)
        for idx, draft in enumerate(non_empty):
            included, reason = self._evaluate_exclusion(draft, idx, total)
            draft.included = included
            draft.exclusion_reason = reason

        # 3) Kısa (dahil) bölümleri komşu dahil bölümle birleştir
        merged = self._merge_short_chapters(non_empty)

        # 4) Nihai başlık kontrolü
        for idx, draft in enumerate(merged):
            if not draft.title.strip():
                draft.title = f"Bölüm {idx + 1}"

        return merged

    def clean_title(self, raw_title: str, fallback_index: int = 1) -> str:
        cleaned = _MULTISPACE_RE.sub(" ", (raw_title or "").strip()).rstrip(".")
        if not cleaned:
            return f"Bölüm {fallback_index}"

        # Tamamı büyük harfli ise (Romen rakamı değilse) Türkçe Başlık Düzenine çevir
        letters = [ch for ch in cleaned if ch.isalpha()]
        if len(letters) >= 4 and cleaned == tr_upper(cleaned) and not re.match(r"^[IVXLCDM]+$", cleaned):
            cleaned = tr_title_case(cleaned)

        return cleaned[:500]

    def _evaluate_exclusion(
        self,
        draft: ChapterDraft,
        index: int,
        total_chapters: int,
    ) -> tuple[bool, str | None]:
        folded_title = fold_turkish(draft.title)
        is_end_region = total_chapters >= 3 and index >= int(total_chapters * 0.85)

        for rule in self._rules:
            pattern = fold_turkish(str(rule.get("pattern", "")))
            if not pattern:
                continue
            pos = str(rule.get("position", "any"))
            if pos == "end_only" and not is_end_region:
                continue

            if folded_title == pattern or folded_title.startswith(f"{pattern} "):
                return False, str(rule.get("reason", "Otomatik hariç tutulan bölüm"))

        # İlk bölüm <= 150 kelime ve ISBN/yayınevi/telif içeriyorsa Künye olarak hariç tut
        if index == 0:
            text = draft.clean_text
            if count_words(text) <= 150:
                folded_body = fold_turkish(text)
                if any(kw in folded_body for kw in _KUNYE_KEYWORDS):
                    return False, "Künye / Telif sayfası"

        return True, None

    def _merge_short_chapters(self, drafts: list[ChapterDraft]) -> list[ChapterDraft]:
        if len(drafts) <= 1 or self.min_chapter_words <= 0:
            return drafts

        result: list[ChapterDraft] = []
        i = 0
        while i < len(drafts):
            curr = drafts[i]
            if not curr.included:
                result.append(curr)
                i += 1
                continue

            words = count_words(curr.clean_text)
            if words < self.min_chapter_words:
                # Sonraki bölüm dahil ise onun başına ekle
                if i + 1 < len(drafts) and drafts[i + 1].included:
                    nxt = drafts[i + 1]
                    nxt.paragraphs = [*curr.paragraphs, *nxt.paragraphs]
                    i += 1
                    continue
                # Son bölümse ve önceki bölüm dahil ise öncekine ekle
                if result and result[-1].included:
                    result[-1].paragraphs.extend(curr.paragraphs)
                    i += 1
                    continue

            result.append(curr)
            i += 1

        return result

    @staticmethod
    def _load_rules(path: Path) -> list[dict[str, Any]]:
        if not path.is_file():
            return []
        with path.open("r", encoding="utf-8") as f:
            data = yaml.safe_load(f)
            return data if isinstance(data, list) else []
