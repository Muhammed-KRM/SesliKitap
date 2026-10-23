from __future__ import annotations

from pathlib import Path

from models.book_models import CleanedDocument, ExtractedDocument
from models.events import ProcessedBookMeta
from utils.turkish import fold_turkish, tr_title_case, tr_upper

_TURKISH_COMMON_WORDS = {"ve", "bir", "bu", "için", "ile", "de", "da", "olan", "olarak", "gibi", "ama", "çok"}
_TURKISH_CHARS = set("ğĞşŞıİçÇöÖüÜ")


class MetadataResolver:
    """Dosya meta verisi, ilk sayfa sezgisi ve dil analizini birleştirerek ProcessedBookMeta üretir."""

    def resolve(
        self,
        doc: ExtractedDocument,
        cleaned: CleanedDocument,
        source_path: Path | None = None,
    ) -> ProcessedBookMeta:
        title = (doc.meta.title or "").strip() or None
        author = (doc.meta.author or "").strip() or None

        # Başlık dosya adıyla aynı veya anlamsızsa ("Microsoft Word - ...", "Untitled") temizle
        if title and (
            title.lower().startswith(("microsoft word", "untitled", "adsız", "isimsiz"))
            or len(title) < 2
        ):
            title = None

        # İlk sayfadaki en büyük yazılı başlığı aday olarak değerlendir
        if title is None and cleaned.paragraphs_by_page and cleaned.paragraphs_by_page[0]:
            first_page_pars = cleaned.paragraphs_by_page[0]
            largest = max(first_page_pars, key=lambda p: p.font_size)
            if largest.font_size >= cleaned.body_font_size * 1.3 and 2 <= len(largest.text.strip()) <= 100:
                candidate = largest.text.strip()
                if candidate == tr_upper(candidate):
                    candidate = tr_title_case(candidate)
                title = candidate

        if title is None and source_path is not None:
            stem = source_path.stem.replace("_", " ").replace("-", " ").strip()
            if stem and not stem.lower().startswith("original"):
                title = stem

        sample_text = " ".join(p.text for p in cleaned.all_paragraphs[:30])
        language = self.detect_language(sample_text, default=doc.meta.language or "tr")

        return ProcessedBookMeta(
            baslik=title,
            yazar=author,
            dil=language,
            yayin_yili=doc.meta.year,
            sayfa_sayisi=doc.meta.page_count or len(doc.pages),
        )

    @staticmethod
    def detect_language(sample_text: str, default: str = "tr") -> str:
        if not sample_text or len(sample_text) < 40:
            return default

        tr_char_hits = sum(1 for ch in sample_text if ch in _TURKISH_CHARS)
        words = [w.strip(".,!?;:\"'()[]") for w in sample_text.lower().split()]
        words = [w for w in words if w]
        if not words:
            return default

        tr_word_hits = sum(1 for w in words if w in _TURKISH_COMMON_WORDS or fold_turkish(w) in _TURKISH_COMMON_WORDS)
        word_ratio = tr_word_hits / len(words)

        if tr_char_hits >= 3 or word_ratio >= 0.04:
            return "tr"
        return default
