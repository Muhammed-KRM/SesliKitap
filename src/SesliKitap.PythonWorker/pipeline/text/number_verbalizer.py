"""Türkçe sayı, sıra sayısı, ondalık ve Romen rakamı okunuş dönüştürücüsü (Bölüm 4.15.6)."""

from __future__ import annotations

import re

from num2words import num2words

_DIGIT_WORDS = {
    "0": "sıfır",
    "1": "bir",
    "2": "iki",
    "3": "üç",
    "4": "dört",
    "5": "beş",
    "6": "altı",
    "7": "yedi",
    "8": "sekiz",
    "9": "dokuz",
}

_ORDINAL_MAP = {
    "bir": "birinci",
    "iki": "ikinci",
    "üç": "üçüncü",
    "dört": "dördüncü",
    "beş": "beşinci",
    "altı": "altıncı",
    "yedi": "yedinci",
    "sekiz": "sekizinci",
    "dokuz": "dokuzuncu",
    "on": "onuncu",
    "yirmi": "yirminci",
    "otuz": "otuzuncu",
    "kırk": "kırkıncı",
    "elli": "ellinci",
    "altmış": "altmışıncı",
    "yetmiş": "yetmişinci",
    "seksen": "sekseninci",
    "doksan": "doksanıncı",
    "yüz": "yüzüncü",
    "bin": "bininci",
    "milyon": "milyonuncu",
    "milyar": "milyarıncı",
    "trilyon": "trilyonuncu",
    "sıfır": "sıfırıncı",
}

_ROMAN_VALUES = {
    "I": 1,
    "V": 5,
    "X": 10,
    "L": 50,
    "C": 100,
    "D": 500,
    "M": 1000,
}

_VALID_ROMAN_RE = re.compile(
    r"^M{0,3}(?:CM|CD|D?C{0,3})(?:XC|XL|L?X{0,3})(?:IX|IV|V?I{0,3})$"
)


def _fix_turkish_cardinal(words: str) -> str:
    """num2words çıktısını doğal Türkçe kurallarına göre düzeltir ('bir yüz' -> 'yüz', 'bir bin' -> 'bin')."""
    # num2words bazı sürümlerde bitişik ("onüç") veya "bir yüz" üretebilir; biz parçalı da doğruluyoruz
    cleaned = re.sub(r"\s+", " ", words).strip()
    cleaned = re.sub(r"^bir\s+yüz\b", "yüz", cleaned)
    cleaned = re.sub(r"^bir\s+bin\b", "bin", cleaned)
    cleaned = re.sub(r"\bbir\s+yüz\b", "yüz", cleaned)
    return cleaned


def _int_to_turkish_words(n: int) -> str:
    """Tam sayıyı boşluklarla ayrılmış standart Türkçe kelimelere çevirir."""
    if n < 0:
        return f"eksi {_int_to_turkish_words(-n)}"
    if n == 0:
        return "sıfır"

    ones = ["", "bir", "iki", "üç", "dört", "beş", "altı", "yedi", "sekiz", "dokuz"]
    tens = ["", "on", "yirmi", "otuz", "kırk", "elli", "altmış", "yetmiş", "seksen", "doksan"]
    scales = [
        (1_000_000_000_000, "trilyon"),
        (1_000_000_000, "milyar"),
        (1_000_000, "milyon"),
        (1_000, "bin"),
    ]

    def _under_thousand(val: int) -> list[str]:
        parts: list[str] = []
        h = val // 100
        rem = val % 100
        t = rem // 10
        u = rem % 10
        if h == 1:
            parts.append("yüz")
        elif h > 1:
            parts.extend([ones[h], "yüz"])
        if t > 0:
            parts.append(tens[t])
        if u > 0:
            parts.append(ones[u])
        return parts

    if n >= 1_000_000_000_000_000:
        # Çok büyük sayılarda num2words geri dönüşü
        try:
            return _fix_turkish_cardinal(num2words(n, lang="tr"))
        except Exception:
            return " ".join(_DIGIT_WORDS.get(ch, ch) for ch in str(n))

    result_parts: list[str] = []
    remaining = n
    for scale_val, scale_name in scales:
        if remaining >= scale_val:
            grp = remaining // scale_val
            remaining %= scale_val
            if scale_val == 1_000 and grp == 1:
                result_parts.append("bin")
            else:
                result_parts.extend(_under_thousand(grp))
                result_parts.append(scale_name)

    if remaining > 0:
        result_parts.extend(_under_thousand(remaining))

    return " ".join(result_parts)


def verbalize_integer(n: int) -> str:
    """Tam sayıyı Türkçe okunuşuna çevirir (ör. 1923 -> 'bin dokuz yüz yirmi üç')."""
    return _int_to_turkish_words(int(n))


def verbalize_ordinal(n: int) -> str:
    """Sıra sayısını Türkçe okunuşuna çevirir (ör. 3 -> 'üçüncü', 19 -> 'on dokuzuncu')."""
    cardinal = _int_to_turkish_words(int(n))
    tokens = cardinal.split()
    if not tokens:
        return cardinal
    last = tokens[-1]
    tokens[-1] = _ORDINAL_MAP.get(last, f"{last}ıncı")
    return " ".join(tokens)


def verbalize_decimal(int_part_str: str, frac_part_str: str, negative: bool = False) -> str:
    """Ondalık sayıyı Türkçe okunuşuna çevirir (ör. '3', '14' -> 'üç virgül on dört')."""
    clean_int = int(int_part_str.replace(".", "")) if int_part_str else 0
    int_words = _int_to_turkish_words(clean_int)
    if negative and clean_int >= 0:
        int_words = f"eksi {int_words}"

    frac_stripped = frac_part_str.strip()
    if not frac_stripped:
        return int_words

    # Başta sıfır varsa ("05" -> "sıfır beş"), yoksa tam sayı gibi ("14" -> "on dört")
    if frac_stripped.startswith("0"):
        leading_zeros = len(frac_stripped) - len(frac_stripped.lstrip("0"))
        rest = frac_stripped.lstrip("0")
        zero_words = ["sıfır"] * leading_zeros
        if rest:
            zero_words.append(_int_to_turkish_words(int(rest)))
        frac_words = " ".join(zero_words)
    else:
        frac_words = _int_to_turkish_words(int(frac_stripped))

    return f"{int_words} virgül {frac_words}"


def verbalize_digits(digit_str: str) -> str:
    """Uzun rakam dizilerini (telefon, ISBN vb.) rakam rakam okunuşa çevirir."""
    words = [_DIGIT_WORDS[ch] for ch in digit_str if ch in _DIGIT_WORDS]
    return " ".join(words)


def roman_to_int(roman: str) -> int | None:
    """Geçerli bir Romen rakamını (I..MMMCMXCIX) tam sayıya çevirir; geçersizse None döner."""
    s = roman.strip().upper()
    if not s or not _VALID_ROMAN_RE.match(s):
        return None

    total = 0
    prev = 0
    for ch in reversed(s):
        val = _ROMAN_VALUES.get(ch)
        if val is None:
            return None
        if val < prev:
            total -= val
        else:
            total += val
            prev = val

    return total if 1 <= total <= 3999 else None
