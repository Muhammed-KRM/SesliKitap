from __future__ import annotations

from enum import StrEnum


class Queues:
    """RabbitMQ kuyruk adları."""

    BOOK_IMPORT = "seslikitap.book-import"
    SYNTHESIS = "seslikitap.synthesis"
    VOICE_PROFILE = "seslikitap.voice-profile"
    EXPORT = "seslikitap.export"


class Exchanges:
    """RabbitMQ exchange adları ve MassTransit önekleri."""

    PREFIX = "SesliKitap.Business.Events:"
    BOOK_IMPORT_REQUESTED = "BookImportRequestedEvent"
    SYNTHESIS_REQUESTED = "SynthesisRequestedEvent"
    VOICE_PROFILE_UPLOADED = "VoiceProfileUploadedEvent"
    BOOK_EXPORT_REQUESTED = "BookExportRequestedEvent"
    BOOK_SOURCE_STORED = "BookSourceStoredEvent"
    BOOK_PROCESSED = "BookProcessedEvent"
    STAGE_CHANGED = "PipelineStageChangedEvent"
    PIPELINE_ERROR = "PipelineErrorEvent"
    VOICE_PROFILE_READY = "VoiceProfileReadyEvent"
    CHAPTER_SYNTHESIZED = "ChapterSynthesizedEvent"
    SYNTHESIS_COMPLETED = "SynthesisCompletedEvent"
    BOOK_EXPORT_COMPLETED = "BookExportCompletedEvent"
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

    @staticmethod
    def voice_reference(voice_profile_id: str) -> str:
        return f"voices/{voice_profile_id}/reference.wav"

    @staticmethod
    def voice_sample(voice_profile_id: str) -> str:
        return f"voices/{voice_profile_id}/sample.ogg"

    @staticmethod
    def chapter_audio(book_id: str, chapter_id: str, version: int, ext: str) -> str:
        clean_ext = ext.lstrip(".").lower()
        return f"books/{book_id}/audio/{chapter_id}/v{version}/chapter.{clean_ext}"

    @staticmethod
    def chapter_timestamps(book_id: str, chapter_id: str, version: int) -> str:
        return f"books/{book_id}/audio/{chapter_id}/v{version}/timestamps.json"

    @staticmethod
    def tts_chunk_object(chunk_hash: str) -> str:
        prefix = chunk_hash[:2] if len(chunk_hash) >= 2 else "00"
        return f"tts-cache/{prefix}/{chunk_hash}.flac"


QUALITY_MS_PER_CHAR_MIN = 25
QUALITY_MS_PER_CHAR_MAX = 160
QUALITY_SILENCE_RMS_DBFS = -50.0
QUALITY_SPEECH_FRAME_DBFS = -40.0
QUALITY_MIN_SPEECH_FRAME_RATIO = 0.25
QUALITY_CLIPPING_THRESHOLD = 0.999
QUALITY_MAX_CLIPPING_RATIO = 0.001
QUALITY_MAX_INTERNAL_SILENCE_SEC = 2.5
# Tonal (sinüs benzeri) sinyal tespiti: 1 sn'lik pencerelerde en güçlü 6 FFT binine düşen enerji oranı.
# Ölçüm: gerçek konuşma 0.2-0.5, saf/harmonik sinüs ~0.89 -> eşik 0.75.
QUALITY_TONAL_WINDOW_SEC = 1.0
QUALITY_TONAL_TOP_BINS = 6
QUALITY_TONAL_RATIO_MAX = 0.75
