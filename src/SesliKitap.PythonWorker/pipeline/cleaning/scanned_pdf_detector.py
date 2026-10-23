from __future__ import annotations

from dataclasses import dataclass

from models.book_models import ExtractedDocument


@dataclass(slots=True)
class ScannedPdfAnalysis:
    """Taranmış PDF analiz sonucu."""

    is_scanned: bool
    scanned_ratio: float
    total_pages: int
    low_text_image_pages: int


class ScannedPdfDetector:
    """PDF belgesinin metin katmanı olmayan taranmış görüntü (OCR gerektiren) olup olmadığını tespit eder."""

    def __init__(self, min_chars_per_page: int = 50, sparse_page_ratio_threshold: float = 0.50) -> None:
        self.min_chars_per_page = min_chars_per_page
        self.sparse_page_ratio_threshold = sparse_page_ratio_threshold

    def analyze(self, doc: ExtractedDocument) -> ScannedPdfAnalysis:
        """Belgeyi analiz ederek ScannedPdfAnalysis döner."""
        if doc.format != "pdf" or not doc.pages:
            return ScannedPdfAnalysis(
                is_scanned=False,
                scanned_ratio=0.0,
                total_pages=len(doc.pages),
                low_text_image_pages=0,
            )

        total_pages = len(doc.pages)
        sparse_pages = 0
        sparse_with_images = 0

        for page in doc.pages:
            if page.char_count < self.min_chars_per_page:
                sparse_pages += 1
                if page.has_images:
                    sparse_with_images += 1

        sparse_ratio = sparse_pages / total_pages
        if sparse_ratio > self.sparse_page_ratio_threshold and sparse_with_images > 0:
            return ScannedPdfAnalysis(
                is_scanned=True,
                scanned_ratio=round(sparse_ratio, 3),
                total_pages=total_pages,
                low_text_image_pages=sparse_with_images,
            )

        total_chars = sum(p.char_count for p in doc.pages)
        if total_chars == 0:
            return ScannedPdfAnalysis(
                is_scanned=True,
                scanned_ratio=1.0,
                total_pages=total_pages,
                low_text_image_pages=sparse_with_images,
            )

        return ScannedPdfAnalysis(
            is_scanned=False,
            scanned_ratio=round(sparse_ratio, 3),
            total_pages=total_pages,
            low_text_image_pages=sparse_with_images,
        )

    def is_scanned(self, doc: ExtractedDocument) -> tuple[bool, float]:
        """(ocr_gerekli_mi, dusuk_metinli_sayfa_orani) ikilisini döner."""
        res = self.analyze(doc)
        return res.is_scanned, res.scanned_ratio
