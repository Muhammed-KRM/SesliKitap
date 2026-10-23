from __future__ import annotations

from models.events import ProcessingError
from pipeline.extraction.base_extractor import BaseExtractor
from pipeline.extraction.epub_extractor import EpubExtractor
from pipeline.extraction.pdf_extractor import PdfExtractor
from pipeline.extraction.txt_extractor import TxtExtractor

_EXTRACTORS: dict[str, type[BaseExtractor]] = {
    "pdf": PdfExtractor,
    "epub": EpubExtractor,
    "txt": TxtExtractor,
}


def get_extractor(fmt: str) -> BaseExtractor:
    """Dosya formatına uygun metin çıkarıcı örneğini döner."""
    normalized = fmt.strip().lstrip(".").lower()
    extractor_cls = _EXTRACTORS.get(normalized)
    if extractor_cls is None:
        raise ProcessingError("UNSUPPORTED_FORMAT", f"Desteklenmeyen kitap dosya formatı: {fmt}")
    return extractor_cls()
