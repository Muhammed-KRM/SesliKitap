"""14 adımlı sıralı Türkçe TTS metin normalizasyon zinciri (Bölüm 4.15.6)."""

from __future__ import annotations

import re
from collections.abc import Iterable
from pathlib import Path

import yaml

from models.synthesis_models import SynthesisInputPronunciationModel
from pipeline.text.number_verbalizer import (
    roman_to_int,
    verbalize_decimal,
    verbalize_digits,
    verbalize_integer,
    verbalize_ordinal,
)
from pipeline.text.pronunciation_applier import PronunciationApplier, PronunciationEntry
from utils.turkish import tr_lower

_RESOURCES_DIR = Path(__file__).resolve().parents[2] / "resources"

_MONTHS_TR = {
    1: "Ocak",
    2: "Şubat",
    3: "Mart",
    4: "Nisan",
    5: "Mayıs",
    6: "Haziran",
    7: "Temmuz",
    8: "Ağustos",
    9: "Eylül",
    10: "Ekim",
    11: "Kasım",
    12: "Aralık",
}

_MONTH_NAMES_PATTERN = (
    r"(?:Ocak|Şubat|Mart|Nisan|Mayıs|Haziran|Temmuz|Ağustos|Eylül|Ekim|Kasım|Aralık|"
    r"ocak|şubat|mart|nisan|mayıs|haziran|temmuz|ağustos|eylül|ekim|kasım|aralık)"
)

_TURKISH_LETTER_PRONUNCIATION = {
    "A": "a",
    "B": "be",
    "C": "ce",
    "Ç": "çe",
    "D": "de",
    "E": "e",
    "F": "fe",
    "G": "ge",
    "Ğ": "yumuşak ge",
    "H": "ha",
    "I": "ı",
    "İ": "i",
    "J": "je",
    "K": "ke",
    "L": "le",
    "M": "me",
    "N": "ne",
    "O": "o",
    "Ö": "ö",
    "P": "pe",
    "R": "re",
    "S": "se",
    "Ş": "şe",
    "T": "te",
    "U": "u",
    "Ü": "ü",
    "V": "ve",
    "Y": "ye",
    "Z": "ze",
    "W": "dabılyu",
    "X": "iks",
    "Q": "ku",
}

_URL_RE = re.compile(r"\b(?:https?://|www\.)\S+\b", re.IGNORECASE)
_EMAIL_RE = re.compile(r"\b[\w.+-]+@[\w-]+\.[\w.-]+\b")

# Para birimi desenleri
_CURRENCY_AFTER_DEC_RE = re.compile(
    r"(?<!\w)(\d{1,3}(?:\.\d{3})*|\d+),(\d{1,2})\s*(TL|TRY|₺|USD|\$|EUR|€|GBP|£)(?!\w)"
)
_CURRENCY_AFTER_INT_RE = re.compile(
    r"(?<!\w)(\d{1,3}(?:\.\d{3})*|\d+)\s*(TL|TRY|₺|USD|\$|EUR|€|GBP|£)(?!\w)"
)
_CURRENCY_BEFORE_DEC_RE = re.compile(
    r"(?<!\w)(₺|\$|€|£)\s*(\d{1,3}(?:\.\d{3})*|\d+),(\d{1,2})(?!\w)"
)
_CURRENCY_BEFORE_INT_RE = re.compile(r"(?<!\w)(₺|\$|€|£)\s*(\d{1,3}(?:\.\d{3})*|\d+)(?!\w)")

_CURRENCY_NAMES = {
    "TL": ("lira", "kuruş"),
    "TRY": ("lira", "kuruş"),
    "₺": ("lira", "kuruş"),
    "USD": ("dolar", "sent"),
    "$": ("dolar", "sent"),
    "EUR": ("avro", "sent"),
    "€": ("avro", "sent"),
    "GBP": ("sterlin", "peni"),
    "£": ("sterlin", "peni"),
}

# Yüzde desenleri
_PERCENT_BEFORE_DEC_RE = re.compile(r"%\s*(\d+),(\d+)")
_PERCENT_BEFORE_INT_RE = re.compile(r"%\s*(\d+)")
_PERCENT_AFTER_DEC_RE = re.compile(r"(?<!\w)(\d+),(\d+)\s*%")
_PERCENT_AFTER_INT_RE = re.compile(r"(?<!\w)(\d+)\s*%")

# Tarih desenleri
_NUMERIC_DATE_RE = re.compile(r"\b(0?[1-9]|[12]\d|3[01])[./](0?[1-9]|1[0-2])[./](\d{4})\b")
_TEXT_DATE_RE = re.compile(
    rf"\b(0?[1-9]|[12]\d|3[01])\s+({_MONTH_NAMES_PATTERN})(?:\s+(\d{{4}}))?\b"
)

# Saat desenleri: 14:30 veya "saat 14.30"
_COLON_TIME_RE = re.compile(r"\b([01]?\d|2[0-3]):([0-5]\d)\b")
_CONTEXT_DOT_TIME_RE = re.compile(
    r"\b(saat|Saat|SAAT)\s+([01]?\d|2[0-3])\.([0-5]\d)(?:['’]([a-zçğıöşü]+))?\b"
)

# Romen rakamı desenleri
_ROMAN_CENTURY_RE = re.compile(
    r"\b([IVXLCDM]{1,7})\.\s*(yüzyıl\w*|asır\w*|asırd\w*|Bölüm|bölüm|Kısım|kısım|Cilt|cilt|Dünya|dünya)\b"
)
_ROMAN_RULER_RE = re.compile(
    r"\b([IVXLCDM]{1,7})\.\s+([A-ZÇĞİÖŞÜ][a-zçğıöşü]{2,})\b"
)
_ROMAN_HEADING_ONLY_RE = re.compile(r"^\s*([IVXLCDM]{1,7})\.?\s*$")

# Sıra sayısı desenleri
_ORDINAL_APOSTROPHE_RE = re.compile(
    r"\b(\d+)['’](?:inci|ıncı|üncü|uncu|ncı|nci|ncu|ncü)\b", re.IGNORECASE
)
_ORDINAL_DOT_BEFORE_LOWER_RE = re.compile(
    r"(?<!\d\.)\b(\d{1,3})\.\s+(?=[a-zçğıöşü]|Bölüm\b|Kısım\b|Cilt\b|Madde\b|Sınıf\b|Kat\b|Yüzyıl\b|Dünya\b)"
)

# Kesme işaretli sayı eki (1923'te, 2000'de, 50'den)
_NUMBER_WITH_SUFFIX_RE = re.compile(
    r"\b(\d{1,3}(?:\.\d{3})+|\d+)['’]([a-zçğıöşüA-ZÇĞİÖŞÜ]+)\b"
)

# Sayı aralığı (1914-1918, 10-15)
_NUMBER_RANGE_RE = re.compile(r"\b(\d{1,4})\s*[-–—]\s*(\d{1,4})\b")

# Binlik ayraçlı tam sayı (1.250.000)
_THOUSANDS_NUMBER_RE = re.compile(r"(?<!\d)\b\d{1,3}(?:\.\d{3})+\b(?!\d)")

# Ondalık sayı (3,14 veya 3.14)
_COMMA_DECIMAL_RE = re.compile(r"(?<!\w)(-?\d+),(\d+)\b")
_DOT_DECIMAL_RE = re.compile(r"(?<!\w)(-?\d+)\.(\d{1,4})\b(?!\.\d)")

# Uzun rakam dizileri (> 12 hane)
_LONG_DIGITS_RE = re.compile(r"\b\d{13,}\b")

# Genel tam sayı
_PLAIN_INTEGER_RE = re.compile(r"(?<!\w)(-?\d+)\b")

# Tamamı büyük harf kelime (>= 5 harf)
_ALL_CAPS_WORD_RE = re.compile(r"\b[A-ZÇĞİÖŞÜ]{5,}\b")


def _load_abbreviation_rules() -> list[dict]:
    yaml_path = _RESOURCES_DIR / "abbreviations_tr.yaml"
    if not yaml_path.exists():
        return []
    return yaml.safe_load(yaml_path.read_text(encoding="utf-8")) or []


_ABBR_RULES = _load_abbreviation_rules()


def remove_urls_emails(text: str) -> str:
    """Kural 1: URL ve e-posta adreslerini metinden temizler."""
    text = _URL_RE.sub("", text)
    text = _EMAIL_RE.sub("", text)
    return text


def normalize_symbols(text: str) -> str:
    """Kural 2: Sembolleri (&, °C, №, §, ×, +, =) okunuş karşılıklarına çevirir."""
    text = re.sub(r"\s*&\s*", " ve ", text)
    text = re.sub(r"°\s*C\b", " santigrat derece", text)
    text = re.sub(r"°\s*F\b", " fahrenhayt derece", text)
    text = text.replace("°", " derece ")
    text = text.replace("№", " numara ")
    text = text.replace("§", " paragraf ")
    text = re.sub(r"(?<=\d)\s*[×x]\s*(?=\d)", " çarpı ", text)
    text = re.sub(r"(?<=\d)\s*\+\s*(?=\d)", " artı ", text)
    text = re.sub(r"(?<=\d)\s*=\s*(?=\d)", " eşittir ", text)
    return text


def expand_abbreviations(text: str) -> str:
    """Kural 3: Kısaltmaları (Dr., Prof., vb., s. 12, 15 km, TBMM, NATO, THY) genişletir."""
    # Önce genel (any) kısaltmalar (uzundan kısaya — M.S., M.Ö., T.C., Prof. vb. önce işlensin)
    any_rules = [
        r for r in _ABBR_RULES if str(r.get("context", "any")).strip() == "any"
    ]
    any_rules.sort(key=lambda r: len(str(r.get("abbr", ""))), reverse=True)

    for rule in any_rules:
        abbr = str(rule.get("abbr", "")).strip()
        expansion = str(rule.get("expansion", "")).strip()
        cs = bool(rule.get("case_sensitive", False))
        if not abbr or not expansion:
            continue

        flags = 0 if cs else re.IGNORECASE
        esc = re.escape(abbr)
        if abbr.endswith("."):
            pattern = rf"(?<![\w.]){esc}(?=\s|$|[,;:!?)”’\]])"
        else:
            pattern = rf"(?<![\w.]){esc}(?!\w)"
        text = re.sub(pattern, expansion, text, flags=flags)

    # Ardından bağlamlı kısaltmalar (before_number / after_number)
    for rule in _ABBR_RULES:
        abbr = str(rule.get("abbr", "")).strip()
        expansion = str(rule.get("expansion", "")).strip()
        context = str(rule.get("context", "any")).strip()
        cs = bool(rule.get("case_sensitive", False))
        if not abbr or not expansion or context == "any":
            continue

        flags = 0 if cs else re.IGNORECASE
        esc = re.escape(abbr)

        if context == "before_number":
            pattern = rf"(?<![\w.]){esc}\s*(?=\d)"
            text = re.sub(pattern, f"{expansion} ", text, flags=flags)
        elif context == "after_number":
            pattern = rf"(?<=\d)\s*{esc}(?!\w)"
            text = re.sub(pattern, f" {expansion}", text, flags=flags)

    # Sözlükte olmayan 2-4 harfli sessiz ağırlıklı büyük harf kısaltmaları harf harf oku
    currency_codes = {"TL", "TRY", "USD", "EUR", "GBP"}

    def _spell_acronym(match: re.Match[str]) -> str:
        word = match.group(0)
        if word in currency_codes:
            return word
        if roman_to_int(word) is not None:
            return word
        vowels = set("AEIİOÖUÜ")
        vowel_count = sum(1 for ch in word if ch in vowels)
        if vowel_count == 0 or (len(word) <= 3 and vowel_count <= 1):
            return " ".join(_TURKISH_LETTER_PRONUNCIATION.get(ch, ch.lower()) for ch in word)
        return word

    text = re.sub(r"\b[A-ZÇĞİÖŞÜ]{2,4}\b", _spell_acronym, text)
    return text


def verbalize_roman_numerals(text: str) -> str:
    """Kural 4: Bağlamlı Romen rakamlarını (IV. Murad, XIX. yüzyıl, tek başına III) okunuşa çevirir."""
    # Tek başına başlık satırı Romen rakamı: "III" veya "IV."
    heading_match = _ROMAN_HEADING_ONLY_RE.match(text)
    if heading_match:
        val = roman_to_int(heading_match.group(1))
        if val is not None:
            return verbalize_integer(val)

    # XIX. yüzyıl / II. Bölüm
    def _repl_century(m: re.Match[str]) -> str:
        roman_str, noun = m.group(1), m.group(2)
        val = roman_to_int(roman_str)
        if val is None:
            return m.group(0)
        return f"{verbalize_ordinal(val)} {noun}"

    text = _ROMAN_CENTURY_RE.sub(_repl_century, text)

    # IV. Murad / II. Abdülhamid / XVI. Louis
    def _repl_ruler(m: re.Match[str]) -> str:
        roman_str, name = m.group(1), m.group(2)
        val = roman_to_int(roman_str)
        if val is None:
            return m.group(0)
        return f"{verbalize_ordinal(val)} {name}"

    text = _ROMAN_RULER_RE.sub(_repl_ruler, text)
    return text


def verbalize_dates(text: str) -> str:
    """Kural 5: Tarihleri (29.10.1923, 29 Ekim 1923) Türkçe okunuşa çevirir."""
    def _repl_numeric_date(m: re.Match[str]) -> str:
        day = int(m.group(1))
        month = int(m.group(2))
        year = int(m.group(3))
        month_name = _MONTHS_TR.get(month, "")
        return f"{verbalize_integer(day)} {month_name} {verbalize_integer(year)}"

    text = _NUMERIC_DATE_RE.sub(_repl_numeric_date, text)

    def _repl_text_date(m: re.Match[str]) -> str:
        day = int(m.group(1))
        month_name = m.group(2)
        year_str = m.group(3)
        if year_str:
            return f"{verbalize_integer(day)} {month_name} {verbalize_integer(int(year_str))}"
        return f"{verbalize_integer(day)} {month_name}"

    text = _TEXT_DATE_RE.sub(_repl_text_date, text)
    return text


def verbalize_times(text: str) -> str:
    """Kural 6: Saat ifadelerini (14:30, saat 14.30) okunuşa çevirir."""
    def _format_hm(h: int, m: int, suffix: str | None = None) -> str:
        h_words = verbalize_integer(h)
        if m == 0:
            base = f"{h_words} sıfır sıfır"
        elif m < 10:
            base = f"{h_words} sıfır {verbalize_integer(m)}"
        else:
            base = f"{h_words} {verbalize_integer(m)}"
        if suffix:
            return f"{base}{tr_lower(suffix)}"
        return base

    def _repl_colon(m: re.Match[str]) -> str:
        return _format_hm(int(m.group(1)), int(m.group(2)))

    text = _COLON_TIME_RE.sub(_repl_colon, text)

    def _repl_context_dot(m: re.Match[str]) -> str:
        prefix_word = m.group(1)
        h = int(m.group(2))
        mins = int(m.group(3))
        suffix = m.group(4)
        return f"{prefix_word} {_format_hm(h, mins, suffix)}"

    text = _CONTEXT_DOT_TIME_RE.sub(_repl_context_dot, text)
    return text


def verbalize_currency(text: str) -> str:
    """Kural 7: Para birimlerini (250 TL, ₺250, 12,50 TL, $5, 5 €) okunuşa çevirir."""
    def _format_currency(int_str: str, frac_str: str | None, code: str) -> str:
        main_unit, sub_unit = _CURRENCY_NAMES.get(code, ("birim", "alt birim"))
        clean_int = int(int_str.replace(".", ""))
        main_words = f"{verbalize_integer(clean_int)} {main_unit}"
        if not frac_str:
            return main_words
        frac_padded = (frac_str + "0")[:2]
        frac_val = int(frac_padded)
        if frac_val == 0:
            return main_words
        return f"{main_words} {verbalize_integer(frac_val)} {sub_unit}"

    text = _CURRENCY_AFTER_DEC_RE.sub(
        lambda m: _format_currency(m.group(1), m.group(2), m.group(3)), text
    )
    text = _CURRENCY_BEFORE_DEC_RE.sub(
        lambda m: _format_currency(m.group(2), m.group(3), m.group(1)), text
    )
    text = _CURRENCY_AFTER_INT_RE.sub(
        lambda m: _format_currency(m.group(1), None, m.group(2)), text
    )
    text = _CURRENCY_BEFORE_INT_RE.sub(
        lambda m: _format_currency(m.group(2), None, m.group(1)), text
    )
    return text


def verbalize_percent(text: str) -> str:
    """Kural 8: Yüzde ifadelerini (%25, %12,5) okunuşa çevirir."""
    text = _PERCENT_BEFORE_DEC_RE.sub(
        lambda m: f"yüzde {verbalize_decimal(m.group(1), m.group(2))}", text
    )
    text = _PERCENT_BEFORE_INT_RE.sub(
        lambda m: f"yüzde {verbalize_integer(int(m.group(1)))}", text
    )
    text = _PERCENT_AFTER_DEC_RE.sub(
        lambda m: f"yüzde {verbalize_decimal(m.group(1), m.group(2))}", text
    )
    text = _PERCENT_AFTER_INT_RE.sub(
        lambda m: f"yüzde {verbalize_integer(int(m.group(1)))}", text
    )
    return text


def verbalize_ordinals(text: str) -> str:
    """Kural 9: Sıra sayılarını ('3. bölüm', '1'inci') okunuşa çevirir; cümle sonu yıllara dokunmaz."""
    text = _ORDINAL_APOSTROPHE_RE.sub(
        lambda m: verbalize_ordinal(int(m.group(1))), text
    )
    text = _ORDINAL_DOT_BEFORE_LOWER_RE.sub(
        lambda m: f"{verbalize_ordinal(int(m.group(1)))} ", text
    )
    return text


def verbalize_decimals_and_numbers(text: str) -> str:
    """Kural 10: Ondalık sayılar, binlik ayraçlı sayılar, kesme işaretli ekler, aralıklar ve tam sayıları okunuşa çevirir."""
    # 1) Uzun rakam dizileri (> 12 hane, ISBN/barkod vb.)
    text = _LONG_DIGITS_RE.sub(lambda m: verbalize_digits(m.group(0)), text)

    # 2) Kesme işaretli sayı eki: 1923'te -> bin dokuz yüz yirmi üçte
    def _repl_suffixed(m: re.Match[str]) -> str:
        raw_num = m.group(1).replace(".", "")
        suffix = tr_lower(m.group(2))
        words = verbalize_integer(int(raw_num))
        return f"{words}{suffix}"

    text = _NUMBER_WITH_SUFFIX_RE.sub(_repl_suffixed, text)

    # 3) Sayı aralıkları: 1914-1918 -> bin dokuz yüz on dört bin dokuz yüz on sekiz
    def _repl_range(m: re.Match[str]) -> str:
        n1 = int(m.group(1))
        n2 = int(m.group(2))
        return f"{verbalize_integer(n1)} {verbalize_integer(n2)}"

    text = _NUMBER_RANGE_RE.sub(_repl_range, text)

    # 4) Virgüllü ondalık sayılar: 3,14 -> üç virgül on dört
    def _repl_comma_dec(m: re.Match[str]) -> str:
        raw_int = m.group(1)
        neg = raw_int.startswith("-")
        clean_int = raw_int.lstrip("-")
        return verbalize_decimal(clean_int, m.group(2), negative=neg)

    text = _COMMA_DECIMAL_RE.sub(_repl_comma_dec, text)

    # 5) Binlik ayraçlı tam sayılar: 1.250.000 -> bir milyon iki yüz elli bin
    def _repl_thousands(m: re.Match[str]) -> str:
        clean_num = int(m.group(0).replace(".", ""))
        return verbalize_integer(clean_num)

    text = _THOUSANDS_NUMBER_RE.sub(_repl_thousands, text)

    # 6) Noktalı ondalık sayılar (ör. 3.14)
    def _repl_dot_dec(m: re.Match[str]) -> str:
        raw_int = m.group(1)
        neg = raw_int.startswith("-")
        clean_int = raw_int.lstrip("-")
        return verbalize_decimal(clean_int, m.group(2), negative=neg)

    text = _DOT_DECIMAL_RE.sub(_repl_dot_dec, text)

    # 7) Kalan tam sayılar
    def _repl_int(m: re.Match[str]) -> str:
        return verbalize_integer(int(m.group(1)))

    text = _PLAIN_INTEGER_RE.sub(_repl_int, text)
    return text


def normalize_case(text: str) -> str:
    """Kural 12: Tamamı büyük harfli >= 5 harfli kelimeleri küçük harfe çevirir."""
    return _ALL_CAPS_WORD_RE.sub(lambda m: tr_lower(m.group(0)), text)


def normalize_punctuation_for_tts(text: str) -> str:
    """Kural 13: Noktalama işaretlerini TTS prozodi kalitesi için sadeleştirir."""
    # Madde imlerini ve diyalog tirelerini baştan temizle (TTS motoru seslendirmesin)
    text = re.sub(r"^\s*[•·▪▫◦⁃*■►—–\-]\s*", "", text)
    # Üç nokta -> tek karakter '…'
    text = re.sub(r"\.{3,}", "…", text)
    # Parantezleri virgüle çevir
    text = re.sub(r"\s*\(\s*", ", ", text)
    text = re.sub(r"\s*\)\s*", ", ", text)
    # Tırnak işaretlerini temizle (TTS bazen tırnakta tuhaf nefes sesi çıkarır)
    text = re.sub(r"[\"'“”‘’«»]", "", text)
    # Tekrarlanan ünlem/soru işaretlerini sadeleştir
    text = re.sub(r"!{2,}", "!", text)
    text = re.sub(r"\?{2,}", "?", text)
    text = re.sub(r",\s*,+", ",", text)
    text = re.sub(r",\s*([.!?…])", r"\1", text)
    text = text.strip().strip(",")
    text = text.strip()
    if text and text[-1] not in ".!?…":
        text = f"{text}."
    return text


def collapse_whitespace(text: str) -> str:
    """Kural 14: Çoklu boşlukları tek boşluğa indirger ve noktalama öncesi boşluğu siler."""
    text = re.sub(r"\s+", " ", text).strip()
    text = re.sub(r"\s+([,.!?…;:])", r"\1", text)
    return text


class TurkishNormalizer:
    """14 kuralı sırayla uygulayarak görüntü metnini TTS okunuş metnine (spoken_text) dönüştürür."""

    def __init__(
        self,
        lexicon: Iterable[PronunciationEntry | SynthesisInputPronunciationModel | dict] | None = None,
        include_default_lexicon: bool = True,
    ) -> None:
        parsed_entries: list[PronunciationEntry] = []
        if lexicon:
            for item in lexicon:
                if isinstance(item, PronunciationEntry):
                    parsed_entries.append(item)
                elif isinstance(item, SynthesisInputPronunciationModel):
                    parsed_entries.append(
                        PronunciationEntry(
                            word=item.kelime,
                            spoken=item.okunus,
                            case_sensitive=item.buyuk_kucuk_duyarli,
                        )
                    )
                elif isinstance(item, dict):
                    w = str(item.get("kelime") or item.get("word") or "").strip()
                    s = str(item.get("okunus") or item.get("spoken") or "").strip()
                    cs = bool(
                        item.get("buyukKucukDuyarli")
                        or item.get("buyuk_kucuk_duyarli")
                        or item.get("case_sensitive")
                        or False
                    )
                    if w and s:
                        parsed_entries.append(PronunciationEntry(word=w, spoken=s, case_sensitive=cs))

        self._pronunciation = PronunciationApplier(
            user_entries=parsed_entries,
            include_defaults=include_default_lexicon,
        )

    def normalize(self, text: str) -> str:
        """Girdi cümlesini 14 kuraldan geçirerek TTS için hazır Türkçe okunuş metnine çevirir."""
        if not text or not text.strip():
            return ""

        s = text
        s = remove_urls_emails(s)
        s = normalize_symbols(s)
        s = expand_abbreviations(s)
        s = verbalize_roman_numerals(s)
        s = verbalize_dates(s)
        s = verbalize_times(s)
        s = verbalize_currency(s)
        s = verbalize_percent(s)
        s = verbalize_ordinals(s)
        s = verbalize_decimals_and_numbers(s)
        s = self._pronunciation.apply(s)
        s = normalize_case(s)
        s = normalize_punctuation_for_tts(s)
        s = collapse_whitespace(s)

        # Eğer geriye yalnızca noktalama kaldıysa boş döndür
        if not re.search(r"[a-zA-ZçğıöşüÇĞİÖŞÜ0-9]", s):
            return ""
        return s
