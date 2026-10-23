from __future__ import annotations

from abc import ABC, abstractmethod
from collections.abc import Callable
from pathlib import Path
from typing import ClassVar

from models.book_models import ExtractedDocument

ProgressCallback = Callable[[int, str], None]


class BaseExtractor(ABC):
    """Kaynak dosyadan yapısal metin, içindekiler ve meta veri çıkaran sınıfların ortak arayüzü."""

    supported_format: ClassVar[str]

    @abstractmethod
    def extract(self, path: Path, progress: ProgressCallback | None = None) -> ExtractedDocument:
        """Verilen dosya yolundan yapısal belge çıkarır."""
