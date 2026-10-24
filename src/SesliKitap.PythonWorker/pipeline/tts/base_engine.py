"""Worker tarafı TTS motor soyutlaması (Bölüm 4.15.6 — Liskov & Open/Closed)."""

from __future__ import annotations

from abc import ABC, abstractmethod

from models.voice_models import (
    BatchResult,
    EngineInfo,
    PreparedVoice,
    SynthChunkInput,
    VoiceParams,
)


class EngineUnavailableError(RuntimeError):
    """TTS motoruna ulaşılamadı, kota doldu veya tekrar denemeler tükendi."""

    def __init__(self, engine_id: str, message: str, *, quota_exhausted: bool = False) -> None:
        super().__init__(f"[{engine_id}] {message}")
        self.engine_id = engine_id
        self.quota_exhausted = quota_exhausted


class FamilyExhaustedError(RuntimeError):
    """Sabitlenen model ailesindeki tüm motorlar başarısız oldu."""

    def __init__(self, family: str, message: str) -> None:
        super().__init__(message)
        self.family = family


class BaseTtsEngine(ABC):
    """Tüm TTS motorlarının (RemoteHttpTtsEngine, MockTtsEngine) uyması gereken arayüz."""

    engine_id: str
    family: str

    @abstractmethod
    def is_available(self) -> bool:
        """Motorun yapılandırılmış (URL/token mevcut) olup olmadığını döndürür."""

    @abstractmethod
    def info(self) -> EngineInfo:
        """Sunucu bilgi uç noktasından örnekleme hızı ve model revizyonunu döndürür."""

    @abstractmethod
    def has_voice(self, voice_hash: str) -> bool:
        """Referans sesin sunucu önbelleğinde olup olmadığını kontrol eder (HEAD /v1/voices/{hash})."""

    @abstractmethod
    def register_voice(self, voice: PreparedVoice) -> None:
        """Referans sesi sunucuya yükler (PUT /v1/voices/{hash})."""

    @abstractmethod
    def synthesize(
        self,
        chunks: list[SynthChunkInput],
        voice: PreparedVoice,
        params: VoiceParams,
        *,
        request_id: str = "req-default",
        language: str = "tr",
    ) -> BatchResult:
        """Metin parçalarını sentezler ve BatchResult döndürür."""

    def warm_up(self) -> float:
        """Konteyneri ısıtmak için info() çağırır ve geçen süreyi saniye olarak döndürür."""
        import time

        t0 = time.perf_counter()
        self.info()
        return round(time.perf_counter() - t0, 3)
