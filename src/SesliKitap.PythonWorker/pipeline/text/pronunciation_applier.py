"""Telaffuz sözlüğü (varsayılan + kullanıcı) derleyici ve uygulayıcı (Bölüm 4.15.6)."""

from __future__ import annotations

import re
from collections.abc import Iterable
from dataclasses import dataclass
from pathlib import Path

import yaml

from utils.turkish import tr_lower

_RESOURCES_DIR = Path(__file__).resolve().parents[2] / "resources"


@dataclass(slots=True, frozen=True)
class PronunciationEntry:
    """Tek bir telaffuz kuralı."""

    word: str
    spoken: str
    case_sensitive: bool = False


def load_default_lexicon() -> list[PronunciationEntry]:
    """resources/lexicon_tr.yaml içindeki varsayılan telaffuz kurallarını yükler."""
    yaml_path = _RESOURCES_DIR / "lexicon_tr.yaml"
    if not yaml_path.exists():
        return []

    raw_items = yaml.safe_load(yaml_path.read_text(encoding="utf-8")) or []
    entries: list[PronunciationEntry] = []
    for item in raw_items:
        w = str(item.get("word", "")).strip()
        s = str(item.get("spoken", "")).strip()
        cs = bool(item.get("case_sensitive", False))
        if w and s:
            entries.append(PronunciationEntry(word=w, spoken=s, case_sensitive=cs))
    return entries


class PronunciationApplier:
    """Telaffuz girdilerini uzunluğa göre azalan sırada derlenmiş regex ile O(n) tek geçişte uygular."""

    def __init__(
        self,
        user_entries: Iterable[PronunciationEntry] | None = None,
        include_defaults: bool = True,
    ) -> None:
        combined: dict[tuple[str, bool], PronunciationEntry] = {}

        if include_defaults:
            for entry in load_default_lexicon():
                key = (entry.word if entry.case_sensitive else tr_lower(entry.word), entry.case_sensitive)
                combined[key] = entry

        if user_entries:
            for entry in user_entries:
                w = entry.word.strip()
                s = entry.spoken.strip()
                if not w or not s:
                    continue
                normalized_entry = PronunciationEntry(word=w, spoken=s, case_sensitive=entry.case_sensitive)
                # Kullanıcı girdisi varsayılanı ezer
                ci_key = (tr_lower(w), False)
                cs_key = (w, True)
                combined.pop(ci_key, None)
                combined.pop(cs_key, None)
                key = cs_key if entry.case_sensitive else ci_key
                combined[key] = normalized_entry

        all_entries = sorted(combined.values(), key=lambda e: len(e.word), reverse=True)
        self._cs_map: dict[str, str] = {
            e.word: e.spoken for e in all_entries if e.case_sensitive
        }
        self._ci_map: dict[str, str] = {
            tr_lower(e.word): e.spoken for e in all_entries if not e.case_sensitive
        }

        if all_entries:
            escaped = [re.escape(e.word) for e in all_entries]
            pattern = r"(?<!\w)(?:" + "|".join(escaped) + r")(?!\w)"
            self._regex: re.Pattern[str] | None = re.compile(pattern, re.IGNORECASE)
        else:
            self._regex = None

    def apply(self, text: str) -> str:
        """Metindeki eşleşen kelimeleri okunuş karşılıklarıyla değiştirir."""
        if not text or self._regex is None:
            return text

        def _repl(match: re.Match[str]) -> str:
            token = match.group(0)
            if token in self._cs_map:
                return self._cs_map[token]
            lowered = tr_lower(token)
            if lowered in self._ci_map:
                return self._ci_map[lowered]
            return token

        return self._regex.sub(_repl, text)
