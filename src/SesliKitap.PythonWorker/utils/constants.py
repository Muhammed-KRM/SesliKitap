from __future__ import annotations

from enum import StrEnum


class Queues:
    """RabbitMQ kuyruk adları."""

    BOOK_IMPORT = "seslikitap.book-import"
    SYNTHESIS = "seslikitap.synthesis"
    VOICE_PROFILE = "seslikitap.voice-profile"


class Exchanges:
    """RabbitMQ exchange adları ve MassTransit önekleri."""

    PREFIX = "SesliKitap.Business.Events:"
    BOOK_IMPORT_REQUESTED = "BookImportRequestedEvent"
    SYNTHESIS_REQUESTED = "SynthesisRequestedEvent"
    VOICE_PROFILE_UPLOADED = "VoiceProfileUploadedEvent"
    BOOK_SOURCE_STORED = "BookSourceStoredEvent"
    BOOK_PROCESSED = "BookProcessedEvent"
    STAGE_CHANGED = "PipelineStageChangedEvent"
    PIPELINE_ERROR = "PipelineErrorEvent"
    VOICE_PROFILE_READY = "VoiceProfileReadyEvent"
    CHAPTER_SYNTHESIZED = "ChapterSynthesizedEvent"
    SYNTHESIS_COMPLETED = "SynthesisCompletedEvent"
    DLX = "seslikitap.dlx"


class Stage(StrEnum):
    """Pipeline aşama adları (.NET PipelineAsamasi ile birebir)."""

    INDIRME = "Indirme"
    METIN_CIKARMA = "MetinCikarma"
    TEMIZLEME = "Temizleme"
    BOLUM_TESPITI = "BolumTespiti"
    KAPAK_URETIMI = "KapakUretimi"
    YUKLEME = "Yukleme"
    OCR = "Ocr"
    REFERANS_HAZIRLAMA = "ReferansHazirlama"
    CUMLE_BOLME = "CumleBolme"
    NORMALIZASYON = "Normalizasyon"
    SENTEZ = "Sentez"
    SES_BIRLESTIRME = "SesBirlestirme"
    KODLAMA = "Kodlama"
    PAKETLEME = "Paketleme"
    TAMAMLANDI = "Tamamlandi"


class RedisKeys:
    """Redis anahtar üreticileri (.NET CacheKeys ile birebir)."""

    PREFIX = "sk:"

    @staticmethod
    def job_cancel(job_id: str) -> str:
        return f"sk:job:{job_id}:cancel"

    @staticmethod
    def tts_chunk(chunk_hash: str) -> str:
        return f"sk:tts:chunk:{chunk_hash}"


class StoragePaths:
    """MinIO nesne anahtarı üreticileri (.NET StorageKeys ile birebir)."""

    @staticmethod
    def book_source(book_id: str, ext: str) -> str:
        clean_ext = ext.lstrip(".").lower()
        return f"books/{book_id}/source/original.{clean_ext}"

    @staticmethod
    def book_cover(book_id: str) -> str:
        return f"books/{book_id}/cover.jpg"

    @staticmethod
    def book_chapters_json(book_id: str) -> str:
        return f"books/{book_id}/processed/chapters.json"

    @staticmethod
    def book_extraction_report(book_id: str) -> str:
        return f"books/{book_id}/processed/extraction-report.json"


QUALITY_MS_PER_CHAR_MIN = 25
QUALITY_MS_PER_CHAR_MAX = 160
