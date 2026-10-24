from __future__ import annotations

from models.events import DosyaFormati, ProcessingError


class FileValidator:
    """İndirilen kitap dosyasının ilk baytlarını (magic bytes) ve boyutunu doğrular."""

    @staticmethod
    def validate_prefix(prefix: bytes, expected_format: str) -> DosyaFormati:
        """İlk 2 KB içindeki dosya imzasını (magic bytes) beklenen formatla karşılaştırır."""
        if not prefix:
            raise ProcessingError("DOWNLOAD_INVALID_FILE", "İndirilen dosya boş.", recoverable=False)

        fmt = expected_format.lower().lstrip(".")

        if fmt == "pdf":
            if b"%PDF-" not in prefix[:1024]:
                raise ProcessingError(
                    "DOWNLOAD_INVALID_FILE",
                    "İndirilen dosya geçerli bir PDF imzası (%PDF-) içermiyor.",
                    recoverable=False,
                )
            return DosyaFormati.PDF

        if fmt == "epub":
            if not prefix.startswith(b"PK\x03\x04"):
                raise ProcessingError(
                    "DOWNLOAD_INVALID_FILE",
                    "İndirilen dosya geçerli bir EPUB/ZIP arşivi değil.",
                    recoverable=False,
                )
            return DosyaFormati.EPUB

        if fmt == "txt":
            if b"\x00" in prefix[:2048]:
                raise ProcessingError(
                    "DOWNLOAD_INVALID_FILE",
                    "İndirilen metin dosyası ikili (binary) NUL baytları içeriyor.",
                    recoverable=False,
                )
            return DosyaFormati.TXT

        raise ProcessingError(
            "DOWNLOAD_INVALID_FILE",
            f"Desteklenmeyen dosya formatı: {expected_format}",
            recoverable=False,
        )
