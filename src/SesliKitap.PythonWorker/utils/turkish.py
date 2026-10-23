from __future__ import annotations

import re

TURKISH_LOWER = "abcçdefgğhıijklmnoöprsştuüvyz"
TURKISH_UPPER = "ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZ"

_LOWER_MAP = str.maketrans({"I": "ı", "İ": "i"})
_UPPER_MAP = str.maketrans({"i": "İ", "ı": "I"})
_FOLD_MAP = str.maketrans(
    {
        "ç": "c",
        "ğ": "g",
        "ı": "i",
        "ö": "o",
        "ş": "s",
        "ü": "u",
        "â": "a",
        "î": "i",
        "û": "u",
    }
)
_SMALL_CONJUNCTIONS = {"ve", "ile", "ya", "da", "de", "ki", "mi", "mı", "mu", "mü", "bir", "için"}
_WORD_RE = re.compile(r"[^\W_]+(?:['’][^\W_]+)*", re.UNICODE)


def tr_lower(text: str) -> str:
    """Metni Türkçe kurallarına göre (I->ı, İ->i) küçük harfe çevirir."""
    if not text:
        return ""
    return text.translate(_LOWER_MAP).lower()


def tr_upper(text: str) -> str:
    """Metni Türkçe kurallarına göre (i->İ, ı->I) büyük harfe çevirir."""
    if not text:
        return ""
    return text.translate(_UPPER_MAP).upper()


def tr_capitalize_word(word: str) -> str:
    """Tek bir kelimeyi Türkçe kurallarına göre ilk harfi büyük yapar."""
    if not word:
        return ""
    first = tr_upper(word[0])
    rest = tr_lower(word[1:])
    return first + rest


def tr_title_case(text: str) -> str:
    """Tamamı büyük harfli Türkçe başlıkları okunabilir Başlık Düzenine çevirir."""
    if not text:
        return ""
    words = text.strip().split()
    result: list[str] = []
    for idx, word in enumerate(words):
        lower = tr_lower(word)
        if idx > 0 and lower in _SMALL_CONJUNCTIONS:
            result.append(lower)
        else:
            result.append(tr_capitalize_word(word))
    return " ".join(result)


def fold_turkish(text: str) -> str:
    """Türkçe karakterleri ASCII karşılıklarına katlar ve küçük harfe çevirir."""
    if not text:
        return ""
    lowered = tr_lower(text.strip())
    folded = lowered.translate(_FOLD_MAP)
    return re.sub(r"\s+", " ", folded).strip()


def count_words(text: str) -> int:
    """Metindeki toplam kelime sayısını döner."""
    if not text:
        return 0
    return len(_WORD_RE.findall(text))
