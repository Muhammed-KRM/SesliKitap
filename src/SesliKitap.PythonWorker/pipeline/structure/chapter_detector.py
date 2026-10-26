from __future__ import annotations

import re

from models.book_models import (
    ChapterDraft,
    CleanedDocument,
    ExtractedDocument,
    ParagraphWithStyle,
)
from utils.turkish import count_words, fold_turkish

_CHAPTER_HEADING_PATTERNS = [
    re.compile(
        r"^(?:BÖLÜM|Bölüm|KISIM|Kısım|ÜNİTE|Ünite|KİTAP|Kitap|FASIL|Fasıl)\s+(?:\d+|[IVXLCDM]+|[A-ZÇĞİÖŞÜa-zçğıöşü]+)\b"
    ),
    re.compile(
        r"^(?:BİRİNCİ|İKİNCİ|ÜÇÜNCÜ|DÖRDÜNCÜ|BEŞİNCİ|ALTINCI|YEDİNCİ|SEKİZİNCİ|DOKUZUNCU|ONUNCU|"
        r"ON\s+BİRİNCİ|ON\s+İKİNCİ|ON\s+ÜÇÜNCÜ|ON\s+DÖRDÜNCÜ|ON\s+BEŞİNCİ|YİRMİNCİ)\s+(?:BÖLÜM|KISIM)\b",
        re.IGNORECASE,
    ),
    re.compile(r"^[IVXLCDM]{1,6}\.?$"),
    re.compile(r"^\d{1,2}\.$"),
]


class ChapterDetector:
    """Çok stratejili (EPUB TOC -> PDF Outline -> Başlık Sezgisi -> Sabit Kelime Boyutu) bölüm tespitçisi."""

    def __init__(self, pseudo_chapter_words: int = 4000) -> None:
        self.pseudo_chapter_words = max(500, pseudo_chapter_words)

    def detect(
        self,
        doc: ExtractedDocument,
        cleaned: CleanedDocument,
    ) -> tuple[list[ChapterDraft], str]:
        """(bolum_taslaklari, kullanilan_yontem) ikilisini döner."""
        # Strateji 1: EPUB TOC veya HTML bölümleri
        if doc.format == "epub":
            epub_chapters = self._detect_epub_toc(doc, cleaned)
            if len(epub_chapters) >= 2:
                return epub_chapters, "epub_toc"

        # Strateji 2: PDF Outline (yer imi ağacı)
        if doc.format == "pdf" and len(doc.toc) >= 2:
            outline_chapters = self._detect_pdf_outline(doc, cleaned)
            if len(outline_chapters) >= 2:
                return outline_chapters, "pdf_outline"

        # Strateji 3: Başlık Sezgisi (font boyutu, kalınlık veya bölüm deseni)
        heuristic_chapters = self._detect_heading_heuristic(cleaned)
        if len(heuristic_chapters) >= 2:
            return heuristic_chapters, "heading_heuristic"

        # Strateji 4: Sabit Kelime Boyutu (paragraf sınırında)
        fixed_chapters = self._detect_fixed_size(cleaned)
        return fixed_chapters, "fixed_size"

    @staticmethod
    def _detect_epub_toc(doc: ExtractedDocument, cleaned: CleanedDocument) -> list[ChapterDraft]:
        if not doc.html_sections:
            return []

        top_level_toc = [e for e in doc.toc if e.level == 1 and e.page_index is not None]
        if len(top_level_toc) >= 2:
            drafts: list[ChapterDraft] = []
            sorted_entries = sorted(top_level_toc, key=lambda e: e.page_index or 0)
            total_pages = len(cleaned.paragraphs_by_page)

            for i, entry in enumerate(sorted_entries):
                start_page = entry.page_index or 0
                end_page = (
                    sorted_entries[i + 1].page_index
                    if i + 1 < len(sorted_entries) and sorted_entries[i + 1].page_index is not None
                    else total_pages
                )
                if end_page is not None and end_page <= start_page and i + 1 < len(sorted_entries):
                    end_page = start_page + 1

                pars: list[str] = []
                for p_idx in range(start_page, min(end_page or total_pages, total_pages)):
                    for par in cleaned.paragraphs_by_page[p_idx]:
                        if not pars and fold_turkish(par.text) == fold_turkish(entry.title):
                            continue
                        pars.append(par.text)

                if pars:
                    drafts.append(ChapterDraft(title=entry.title, paragraphs=pars))

            if len(drafts) >= 2:
                return drafts

        # TOC yoksa her HTML section bir bölüm
        drafts = []
        for idx, sec in enumerate(doc.html_sections):
            if idx >= len(cleaned.paragraphs_by_page):
                break
            page_pars = [p.text for p in cleaned.paragraphs_by_page[idx] if p.text.strip()]
            if not page_pars:
                continue
            title = sec.title or f"Bölüm {len(drafts) + 1}"
            if page_pars and fold_turkish(page_pars[0]) == fold_turkish(title):
                page_pars = page_pars[1:]
            if page_pars:
                drafts.append(ChapterDraft(title=title, paragraphs=page_pars))

        return drafts

    @staticmethod
    def _detect_pdf_outline(doc: ExtractedDocument, cleaned: CleanedDocument) -> list[ChapterDraft]:
        min_level = min(e.level for e in doc.toc)
        target_entries = [e for e in doc.toc if e.level == min_level and e.page_index is not None]
        if len(target_entries) < 2:
            target_entries = [e for e in doc.toc if e.page_index is not None]
        if len(target_entries) < 2:
            return []

        total_pages = len(cleaned.paragraphs_by_page)
        drafts: list[ChapterDraft] = []

        # İlk outline girişinden önceki sayfalar (Önsöz / Giriş varsa)
        first_page = target_entries[0].page_index or 0
        if first_page > 0:
            pre_pars = [
                p.text
                for p_idx in range(0, min(first_page, total_pages))
                for p in cleaned.paragraphs_by_page[p_idx]
                if p.text.strip()
            ]
            if count_words(" ".join(pre_pars)) >= 150:
                drafts.append(ChapterDraft(title="Giriş", paragraphs=pre_pars))

        for i, entry in enumerate(target_entries):
            start_page = entry.page_index or 0
            next_page = (
                target_entries[i + 1].page_index
                if i + 1 < len(target_entries) and target_entries[i + 1].page_index is not None
                else total_pages
            )
            end_page = max(start_page + 1, next_page or total_pages)

            pars: list[str] = []
            for p_idx in range(start_page, min(end_page, total_pages)):
                for par in cleaned.paragraphs_by_page[p_idx]:
                    if not pars and fold_turkish(par.text) == fold_turkish(entry.title):
                        continue
                    pars.append(par.text)

            if pars:
                drafts.append(ChapterDraft(title=entry.title, paragraphs=pars))

        return drafts

    def _detect_heading_heuristic(self, cleaned: CleanedDocument) -> list[ChapterDraft]:
        all_pars = cleaned.all_paragraphs
        if not all_pars:
            return []

        body_size = cleaned.body_font_size
        heading_indices: list[int] = []

        for idx, par in enumerate(all_pars):
            if self._is_candidate_heading(par, body_size):
                heading_indices.append(idx)

        if len(heading_indices) < 2:
            return []

        drafts: list[ChapterDraft] = []
        if heading_indices[0] > 0:
            intro_pars = [p.text for p in all_pars[: heading_indices[0]] if p.text.strip()]
            if intro_pars:
                drafts.append(ChapterDraft(title="Giriş", paragraphs=intro_pars))

        for i, h_idx in enumerate(heading_indices):
            next_h_idx = heading_indices[i + 1] if i + 1 < len(heading_indices) else len(all_pars)
            title = all_pars[h_idx].text.strip()
            body_pars = [p.text for p in all_pars[h_idx + 1 : next_h_idx] if p.text.strip()]

            # Arka arkaya iki başlık satırı geldiyse ("BÖLÜM 1" + "Yolculuk") başlıkları birleştir
            if not body_pars and i + 1 < len(heading_indices) and heading_indices[i + 1] == h_idx + 1:
                all_pars[h_idx + 1].text = f"{title} — {all_pars[h_idx + 1].text.strip()}"
                continue

            if body_pars:
                drafts.append(ChapterDraft(title=title, paragraphs=body_pars))

        if len(drafts) < 2:
            return []

        total_words = sum(count_words(d.clean_text) for d in drafts)
        avg_words = total_words / len(drafts)
        # Küçük test belgelerinde veya gerçek kitaplarda makul ortalama kelime kontrolü
        min_avg = 80 if total_words < 2000 else 350
        if avg_words < min_avg or (len(drafts) > 80 and total_words < 50000):
            return []

        return drafts

    @staticmethod
    def _is_candidate_heading(par: ParagraphWithStyle, body_size: float) -> bool:
        txt = par.text.strip()
        if not txt or len(txt) > 90:
            return False

        words = txt.split()
        if len(words) > 10:
            return False

        if txt.endswith((".", ",", ";", "?", "!")) and not (
            _CHAPTER_HEADING_PATTERNS[2].match(txt) or _CHAPTER_HEADING_PATTERNS[3].match(txt)
        ):
            return False

        if not any(ch.isalpha() for ch in txt):
            return bool(_CHAPTER_HEADING_PATTERNS[3].match(txt) and par.font_size >= body_size * 1.35)

        # Üstbilgi, dipnot veya gövde altı fontlar başlık olamaz
        if par.font_size < body_size * 1.15:
            return False

        matches_pattern = any(pat.match(txt) is not None for pat in _CHAPTER_HEADING_PATTERNS)
        is_large = par.is_heading_tag or (par.font_size >= body_size * 1.30)
        is_bold_title = par.is_bold and par.font_size >= body_size * 1.20 and len(words) <= 6

        if matches_pattern and (is_large or is_bold_title):
            return True

        return bool(is_large and 1 <= len(words) <= 8 and len(txt) >= 3)

    def _detect_fixed_size(self, cleaned: CleanedDocument) -> list[ChapterDraft]:
        all_pars = [p.text for p in cleaned.all_paragraphs if p.text.strip()]
        if not all_pars:
            return []

        drafts: list[ChapterDraft] = []
        current_pars: list[str] = []
        current_words = 0

        for par_text in all_pars:
            w = count_words(par_text)
            if current_pars and (current_words + w) > self.pseudo_chapter_words:
                drafts.append(
                    ChapterDraft(
                        title=f"Bölüm {len(drafts) + 1}",
                        paragraphs=current_pars,
                    )
                )
                current_pars = [par_text]
                current_words = w
            else:
                current_pars.append(par_text)
                current_words += w

        if current_pars:
            drafts.append(
                ChapterDraft(
                    title=f"Bölüm {len(drafts) + 1}",
                    paragraphs=current_pars,
                )
            )

        return drafts
