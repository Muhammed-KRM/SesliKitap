"""Türkçe kurallı cümle ve paragraf bölücü (Bölüm 4.15.6)."""

from __future__ import annotations

import re
from pathlib import Path

import yaml

from models.synthesis_models import Paragraph, Sentence

_RESOURCES_DIR = Path(__file__).resolve().parents[2] / "resources"

_DIALOGUE_PREFIX_RE = re.compile(r"^\s*[—–\-]\s*\S")
_SINGLE_INITIAL_RE = re.compile(r"(?:^|\s)[A-ZÇĞİÖŞÜ]\.$")
_MULTI_DOT_ABBR_RE = re.compile(r"(?:^|\s)(?:[A-ZÇĞİÖŞÜa-zçğıöşü]{1,3}\.){2,}$")
_ROMAN_DOT_RE = re.compile(r"(?:^|\s)[IVXLCDM]{1,7}\.$")
_NUMBER_DOT_RE = re.compile(r"(?:^|\s)\d+\.$")
_DATE_OR_DECIMAL_PREFIX_RE = re.compile(r"\d+\.\d*\.$")
_LOWERCASE_START_RE = re.compile(r"^[\"'”’)\]]*\s*[a-zçğıöşü]")
_SENTENCE_START_RE = re.compile(
    r"^[\s]+(?:[\"'“‘«(\[]*\s*[A-ZÇĞİÖŞÜ0-9]|[—–\-]\s*[A-ZÇĞİÖŞÜa-zçğıöşü\"'“‘])"
)
_CLOSING_QUOTES = set("\"'”’»)]}")


def _load_default_abbreviations() -> tuple[set[str], set[str]]:
    """abbreviations_tr.yaml dosyasından nokta ile biten kısaltmaları yükler."""
    case_sensitive: set[str] = set()
    case_insensitive: set[str] = set()

    yaml_path = _RESOURCES_DIR / "abbreviations_tr.yaml"
    if yaml_path.exists():
        raw_items = yaml.safe_load(yaml_path.read_text(encoding="utf-8")) or []
        for item in raw_items:
            abbr = str(item.get("abbr", "")).strip()
            if not abbr.endswith("."):
                continue
            if item.get("case_sensitive", False):
                case_sensitive.add(abbr)
                case_insensitive.add(abbr.lower())
            else:
                case_insensitive.add(abbr.lower())

    # Ek güvenlik kümesi
    for extra in (
        "dr.",
        "prof.",
        "doç.",
        "yrd.",
        "av.",
        "op.",
        "sn.",
        "hz.",
        "vb.",
        "vs.",
        "vd.",
        "bkz.",
        "bk.",
        "krş.",
        "çev.",
        "haz.",
        "der.",
        "ed.",
        "yay.",
        "bs.",
        "s.",
        "c.",
        "sy.",
        "yy.",
        "y.",
        "no.",
        "tel.",
        "ör.",
        "örn.",
        "yak.",
        "ort.",
        "maks.",
        "min.",
        "mah.",
        "cad.",
        "cd.",
        "sok.",
        "sk.",
        "apt.",
        "ltd.",
        "şti.",
        "a.ş.",
        "m.ö.",
        "m.s.",
        "t.c.",
        "ing.",
        "fr.",
        "alm.",
        "ar.",
        "far.",
        "osm.",
        "lat.",
    ):
        case_insensitive.add(extra)

    return case_sensitive, case_insensitive


_CS_ABBRS, _CI_ABBRS = _load_default_abbreviations()


class SentenceSegmenter:
    """Temizlenmiş bölüm metnini paragraf ve cümlelere ayırır."""

    def __init__(self, extra_abbreviations: set[str] | None = None) -> None:
        self._ci_abbrs = set(_CI_ABBRS)
        if extra_abbreviations:
            for item in extra_abbreviations:
                cleaned = item.strip().lower()
                if cleaned:
                    self._ci_abbrs.add(cleaned if cleaned.endswith(".") else f"{cleaned}.")

    def split_paragraphs(self, text: str, start_index: int = 0) -> list[Paragraph]:
        """Metni paragraf ve cümle listesine böler; cümle indeksleri 0-tabanlı ardışıktır."""
        if not text or not text.strip():
            return []

        normalized = text.replace("\r\n", "\n").replace("\r", "\n")
        raw_paragraphs = re.split(r"\n\s*\n+", normalized)

        paragraphs: list[Paragraph] = []
        sentence_idx = start_index
        paragraph_idx = 0

        for raw_para in raw_paragraphs:
            raw_para_stripped = raw_para.strip()
            if not raw_para_stripped:
                continue

            # Paragraf içinde satır başı diyalog tirelerini ayrı alt-bloklara ayır
            lines = [ln.strip() for ln in raw_para_stripped.split("\n") if ln.strip()]
            sub_blocks: list[str] = []
            for ln in lines:
                if sub_blocks and (_DIALOGUE_PREFIX_RE.match(ln) or _DIALOGUE_PREFIX_RE.match(sub_blocks[-1])):
                    sub_blocks.append(ln)
                elif sub_blocks:
                    sub_blocks[-1] = f"{sub_blocks[-1]} {ln}"
                else:
                    sub_blocks.append(ln)

            para_sentences: list[Sentence] = []
            for block in sub_blocks:
                is_dialogue_block = bool(_DIALOGUE_PREFIX_RE.match(block))
                raw_sentences = self._split_block_into_sentences(block)
                for s_text in raw_sentences:
                    cleaned = s_text.strip()
                    if not cleaned:
                        continue
                    is_diag = is_dialogue_block or bool(_DIALOGUE_PREFIX_RE.match(cleaned))
                    para_sentences.append(
                        Sentence(
                            index=sentence_idx,
                            paragraph=paragraph_idx,
                            display=cleaned,
                            spoken="",
                            is_dialogue=is_diag,
                        )
                    )
                    sentence_idx += 1

            if para_sentences:
                paragraphs.append(Paragraph(index=paragraph_idx, sentences=para_sentences))
                paragraph_idx += 1

        return paragraphs

    def split_sentences(self, text: str) -> list[Sentence]:
        """Paragrafları düzleştirerek sıralı cümle listesi döndürür."""
        paragraphs = self.split_paragraphs(text)
        return [s for p in paragraphs for s in p.sentences]

    def _split_block_into_sentences(self, block: str) -> list[str]:
        n = len(block)
        sentences: list[str] = []
        start = 0
        i = 0

        while i < n:
            ch = block[i]
            if ch in ".!?…":
                # Ardışık noktaları / ünlem-soru işaretlerini tüket (ör. "...", "?!")
                end_punct = i
                while end_punct + 1 < n and block[end_punct + 1] in ".!?…":
                    end_punct += 1

                punct_token = block[i : end_punct + 1]

                # Kapanış tırnak veya parantez varsa cümleye dahil et
                after_punct = end_punct
                while after_punct + 1 < n and block[after_punct + 1] in _CLOSING_QUOTES:
                    after_punct += 1

                if self._should_split(block, start, i, end_punct, after_punct, punct_token):
                    segment = block[start : after_punct + 1].strip()
                    if segment:
                        sentences.append(segment)
                    start = after_punct + 1
                    while start < n and block[start].isspace():
                        start += 1
                    i = start
                    continue

                i = after_punct + 1
                continue

            i += 1

        tail = block[start:].strip()
        if tail:
            sentences.append(tail)

        return sentences

    def _should_split(
        self,
        block: str,
        seg_start: int,
        punct_start: int,
        punct_end: int,
        after_close: int,
        punct_token: str,
    ) -> bool:
        n = len(block)
        if after_close + 1 >= n:
            return True

        # Noktalamadan hemen sonra boşluk yoksa (ör. 3.14, 12.30, 29.10.1923, e-posta/URL) bölme
        next_char = block[after_close + 1]
        if not next_char.isspace():
            return False

        rest = block[after_close + 1 :]
        rest_stripped = rest.lstrip()
        if not rest_stripped:
            return True

        # Üç nokta ("..." veya "…") sonrası küçük harfle devam ediyorsa bölme
        if ("…" in punct_token or punct_token.startswith("..")) and _LOWERCASE_START_RE.match(
            rest_stripped
        ):
            return False

        # Tek nokta özel durumları
        if punct_token == ".":
            prefix = block[seg_start : punct_end + 1].rstrip()

            # Son kelimeyi çıkar
            last_token_match = re.search(r"(\S+)$", prefix)
            last_token = last_token_match.group(1).lstrip("\"'“‘([") if last_token_match else ""
            last_token_lower = last_token.lower()

            # 1) Kısaltma sözlüğü kontrolü
            if last_token_lower in self._ci_abbrs:
                return False

            # 2) Çok noktalı kısaltma (M.Ö., T.C., A.Ş., v.b.)
            if _MULTI_DOT_ABBR_RE.search(prefix):
                return False

            # 3) Tek büyük harf + nokta (A. Hamdi Tanpınar)
            if _SINGLE_INITIAL_RE.search(prefix):
                return False

            # 4) Romen rakamı + nokta (IV. Murad, XIX. yüzyıl)
            if _ROMAN_DOT_RE.search(prefix):
                return False

            # 5) Sayı + nokta + küçük harf veya sayı (3. bölüm, 1. madde)
            if _NUMBER_DOT_RE.search(prefix) and (
                _LOWERCASE_START_RE.match(rest_stripped) or rest_stripped[0].isdigit()
            ):
                return False

            # 6) Sonraki cümle küçük harfle başlıyorsa bölme
            if _LOWERCASE_START_RE.match(rest_stripped):
                return False

        # Sonraki anlamlı karakter büyük harf, rakam, açılış tırnağı veya diyalog tiresi mi?
        return bool(_SENTENCE_START_RE.match(rest))
