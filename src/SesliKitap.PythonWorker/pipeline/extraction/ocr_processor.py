"""Taranmış (resim tabanlı) PDF kitaplarına Türkçe OCR katmanı ekleyen bileşen (Faz 8 — Adım 8.4)."""

from __future__ import annotations

from pathlib import Path
from typing import Any

from models.events import ProcessingError
from utils.logger import get_logger

logger = get_logger(__name__)

try:
    import ocrmypdf as _ocrmypdf
except ImportError:
    _ocrmypdf = None


class OcrProcessor:
    """OCRmyPDF (Tesseract Türkçe dil paketi) kullanarak PDF dosyasına aranabilir metin katmanı ekler."""

    def __init__(self, language: str = "tur", jobs: int = 2) -> None:
        self.language = language
        self.jobs = jobs

    def process(self, input_pdf: Path, output_pdf: Path) -> Path:
        """Girdi PDF'e Türkçe OCR uygular ve çıktı PDF yolunu döner."""
        if not input_pdf.is_file():
            raise ProcessingError(
                "OCR_INPUT_NOT_FOUND",
                f"OCR uygulanacak PDF dosyası bulunamadı: {input_pdf.name}",
                recoverable=False,
            )

        output_pdf.parent.mkdir(parents=True, exist_ok=True)

        if _ocrmypdf is None:
            raise ProcessingError(
                "OCR_NOT_AVAILABLE",
                "Sistemde 'ocrmypdf' veya 'tesseract-ocr-tur' bileşeni kurulu değil.",
                recoverable=False,
            )

        logger.info(
            "OCRmyPDF başlatılıyor: input=%s, lang=%s, jobs=%d",
            input_pdf.name,
            self.language,
            self.jobs,
        )
        try:
            ocr_fn: Any = _ocrmypdf.ocr
            ocr_fn(
                input_file=str(input_pdf),
                output_file=str(output_pdf),
                language=self.language,
                skip_text=True,
                deskew=True,
                optimize=1,
                progress_bar=False,
                jobs=self.jobs,
            )
        except ProcessingError:
            raise
        except Exception as exc:
            raise ProcessingError(
                "OCR_FAILED",
                f"OCR işlemi sırasında hata oluştu: {exc}",
                recoverable=False,
            ) from exc

        if not output_pdf.is_file() or output_pdf.stat().st_size == 0:
            raise ProcessingError(
                "OCR_EMPTY_OUTPUT",
                "OCR işlemi tamamlandı ancak çıktı PDF dosyası üretilemedi.",
                recoverable=False,
            )

        logger.info("OCR tamamlandı: %s (%d bayt)", output_pdf.name, output_pdf.stat().st_size)
        return output_pdf
