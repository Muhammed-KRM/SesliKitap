from __future__ import annotations

from datetime import UTC, datetime
from enum import IntEnum
from typing import Any

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel


class ProcessingError(Exception):
    """Pipeline işleme sırasında oluşan beklenen iş hatası."""

    def __init__(self, code: str, message: str, recoverable: bool = False) -> None:
        super().__init__(message)
        self.code = code
        self.message = message
        self.recoverable = recoverable


class ChapterFailedError(Exception):
    """Tek bir bölümün sentezlenmesi sırasında oluşan hata."""

    def __init__(self, code: str, message: str) -> None:
        super().__init__(message)
        self.code = code
        self.message = message


class KaynakTipi(IntEnum):
    """Kitap kaynağı (.NET KaynakTipi ile birebir)."""

    YUKLEME = 0
    INTERNET_ARCHIVE = 1
    OPEN_LIBRARY = 2
    GUTENBERG = 3
    WEB = 4


class DosyaFormati(IntEnum):
    """Kaynak dosya formatı (.NET DosyaFormati ile birebir)."""

    PDF = 0
    EPUB = 1
    TXT = 2


class CamelModel(BaseModel):
    """camelCase JSON serileştirme/ayrıştırma taban modeli."""

    model_config = ConfigDict(
        alias_generator=to_camel,
        populate_by_name=True,
        extra="ignore",
    )


class IntegrationEventBase(CamelModel):
    """Tüm entegrasyon olaylarının ortak alanları."""

    version: int = 1
    trace_id: str | None = None
    occurred_at_utc: datetime = Field(default_factory=lambda: datetime.now(UTC))


class ProcessingSettingsModel(CamelModel):
    """Kitap temizleme ve bölümleme parametreleri."""

    ust_alt_bolge_orani: float = 0.08
    tekrar_esigi: float = 0.30
    min_bolum_kelime: int = 300
    sozde_bolum_kelime: int = 4000
    ocr_uygula: bool = False


class BookImportRequestedEvent(IntegrationEventBase):
    """.NET API tarafından yayınlanan kitap işleme isteği olayı."""

    book_id: str
    kaynak_tipi: KaynakTipi = KaynakTipi.YUKLEME
    orijinal_dosya_yolu: str | None = None
    indirme_url: str | None = None
    dosya_formati: DosyaFormati = DosyaFormati.PDF
    beklenen_format: str = "pdf"
    ayarlar: ProcessingSettingsModel = Field(default_factory=ProcessingSettingsModel)


class BookSourceStoredEvent(IntegrationEventBase):
    """Keşiften indirilen kaynak dosya doğrulandıktan ve MinIO'ya kaydedildikten sonra yayınlanan olay."""

    book_id: str
    orijinal_dosya_yolu: str
    dosya_formati: DosyaFormati
    dosya_boyutu: int
    dosya_hash: str


class ProcessedBookMeta(CamelModel):
    """Çıkarılan kitap meta verisi."""

    baslik: str | None = None
    yazar: str | None = None
    dil: str = "tr"
    yayin_yili: int | None = None
    sayfa_sayisi: int | None = None


class ProcessedBookStats(CamelModel):
    """Temizlik ve bölümleme istatistikleri."""

    silinen_ust_bilgi: int = 0
    silinen_sayfa_no: int = 0
    silinen_dipnot: int = 0
    birlesen_tire: int = 0
    bolum_tespit_yontemi: str = "fixed_size"


class ProcessedChapterItem(CamelModel):
    """chapters.json içindeki tek bir bölüm öğesi."""

    sira_no: int
    baslik: str
    temiz_metin: str
    kelime_sayisi: int
    karakter_sayisi: int
    dahil_mi: bool = True
    otomatik_haric_nedeni: str | None = None


class ProcessedChaptersPayload(CamelModel):
    """MinIO'daki chapters.json kök modeli."""

    book_id: str
    chapters: list[ProcessedChapterItem]


class BookProcessedEvent(IntegrationEventBase):
    """Kitap metin çıkarma ve bölümleme tamamlandığında yayınlanan olay."""

    book_id: str
    bolumler_yolu: str | None = None
    kapak_yolu: str | None = None
    meta: ProcessedBookMeta | None = None
    istatistik: ProcessedBookStats | None = None
    ocr_gerekli: bool = False


class PipelineStageChangedEvent(IntegrationEventBase):
    """Pipeline aşama ilerleme olayı."""

    book_id: str | None = None
    job_id: str | None = None
    chapter_id: str | None = None
    asama: str
    yuzde: int
    mesaj: str
    detay: dict[str, Any] | None = None


class PipelineErrorEvent(IntegrationEventBase):
    """Pipeline hata olayı."""

    book_id: str | None = None
    job_id: str | None = None
    chapter_id: str | None = None
    voice_profile_id: str | None = None
    asama: str
    hata_kodu: str
    hata_mesaji: str
    kurtarilabilir: bool = False


class VoiceProfileUploadedEvent(IntegrationEventBase):
    """.NET API tarafından yayınlanan ses profili hazırlama ve örnek ses üretme isteği."""

    voice_profile_id: str
    ham_ses_yolu: str
    referans_metin: str | None = None
    ornek_metin: str


class VoiceProfileReadyEvent(IntegrationEventBase):
    """Python Worker ses profilini hazırladığında yayınlanan olay."""

    voice_profile_id: str
    basarili: bool
    referans_ses_yolu: str | None = None
    referans_hash: str | None = None
    referans_sure_sn: float | None = None
    ornek_ses_yolu: str | None = None
    gpu_ms: int = 0
    hata_mesaji: str | None = None


class SynthesisRequestedEvent(IntegrationEventBase):
    """.NET API tarafından yayınlanan kitap seslendirme (sentez) işi başlatma olayı."""

    job_id: str
    book_id: str
    girdi_yolu: str


class ChapterSynthesizedEvent(IntegrationEventBase):
    """Tek bir bölümün seslendirmesi tamamlandığında yayınlanan olay."""

    job_id: str
    book_id: str
    chapter_id: str
    metin_surumu: int
    opus_yolu: str
    mp3_yolu: str
    zaman_damgasi_yolu: str
    opus_boyutu: int
    mp3_boyutu: int
    sure_sn: float
    cumle_sayisi: int
    motor: str
    gpu_ms: int = 0
    onbellek_parca: int = 0
    uretilen_parca: int = 0


class SynthesisCompletedEvent(IntegrationEventBase):
    """Sentez işinin tüm bölümleri tamamlandığında (veya iptal edildiğinde) yayınlanan olay."""

    job_id: str
    book_id: str
    sonuc: str = "Tamamlandi"
    tamamlanan_bolum: int = 0
    hatali_bolum: int = 0
    iptal_edildi: bool = False
    toplam_gpu_ms: int = 0
    uretilen_ses_sn: float = 0.0
    kullanilan_motorlar: str | None = None


class BookExportChapterItem(CamelModel):
    """M4B dışa aktarımına dahil edilecek tekil bölüm bilgisi (Faz 8)."""

    sira_no: int
    baslik: str
    ses_yolu: str
    sure_sn: float = 0.0


class BookExportRequestedEvent(IntegrationEventBase):
    """.NET API tarafından yayınlanan M4B sesli kitap dışa aktarım üretim isteği olayı (Faz 8)."""

    book_id: str
    baslik: str
    yazar: str | None = None
    kapak_yolu: str | None = None
    cikti_yolu: str
    bolumler: list[BookExportChapterItem] = Field(default_factory=list)


class BookExportCompletedEvent(IntegrationEventBase):
    """M4B dışa aktarım üretimi tamamlandığında yayınlanan olay (Faz 8)."""

    book_id: str
    format: str = "m4b"
    basarili: bool = True
    dosya_yolu: str | None = None
    boyut: int = 0
    hata_mesaji: str | None = None

