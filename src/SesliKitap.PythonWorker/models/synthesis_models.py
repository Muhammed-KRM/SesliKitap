"""Kitap seslendirme (sentez) pipeline'ı veri modelleri (Bölüm 4.15.6)."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

import numpy as np
from pydantic import Field

from models.events import CamelModel
from models.voice_models import VoiceParams


class SynthesisInputBookModel(CamelModel):
    """input.json içindeki kitap meta bilgisi."""

    id: str
    baslik: str
    yazar: str | None = None
    kapak_yolu: str | None = None


class SynthesisInputVoiceParamsModel(CamelModel):
    """input.json içindeki ses profili parametreleri."""

    cfg_value: float = 2.0
    inference_timesteps: int = 10
    konusma_hizi: float = 1.0
    tohum: int | None = None

    def to_voice_params(self) -> VoiceParams:
        """VoiceParams dataclass'ına dönüştürür."""
        return VoiceParams(
            cfg_value=self.cfg_value,
            inference_timesteps=self.inference_timesteps,
            konusma_hizi=self.konusma_hizi,
            seed=self.tohum,
        )


class SynthesisInputVoiceProfileModel(CamelModel):
    """input.json içindeki ses profili bilgisi."""

    id: str
    ad: str
    referans_ses_yolu: str
    referans_hash: str
    referans_metin: str | None = None
    parametreler: SynthesisInputVoiceParamsModel = Field(
        default_factory=SynthesisInputVoiceParamsModel
    )


class SynthesisInputSettingsModel(CamelModel):
    """input.json içindeki duraklama ve ses kodlama ayarları."""

    cumle_arasi_ms: int = 0
    paragraf_arasi_ms: int = 100
    baslik_sonrasi_ms: int = 400
    diyalog_ms: int = 50
    bolum_basligi_oku: bool = True
    opus_bitrate_kbps: int = 48
    mp3_bitrate_kbps: int = 64
    hedef_lufs: float = -18.0


class SynthesisInputPronunciationModel(CamelModel):
    """input.json içindeki telaffuz sözlüğü girdisi."""

    kelime: str
    okunus: str
    buyuk_kucuk_duyarli: bool = False


class SynthesisInputChapterModel(CamelModel):
    """input.json içindeki seslendirilecek bölüm."""

    id: str
    sira_no: int
    baslik: str
    metin_surumu: int
    temiz_metin: str
    ses_surumu: int | None = None
    onceki_ses_metin_surumu: int | None = None


class SynthesisInput(CamelModel):
    """MinIO'daki synthesis/{jobId}/input.json kök modeli (Bölüm 4.7.1)."""

    job_id: str
    book: SynthesisInputBookModel
    voice_profile: SynthesisInputVoiceProfileModel
    motor_sirasi: list[str] = Field(default_factory=lambda: ["voxcpm2_modal"])
    ayarlar: SynthesisInputSettingsModel = Field(default_factory=SynthesisInputSettingsModel)
    telaffuz_sozlugu: list[SynthesisInputPronunciationModel] = Field(default_factory=list)
    chapters: list[SynthesisInputChapterModel] = Field(default_factory=list)


@dataclass(slots=True)
class Sentence:
    """Segmentasyon ve normalizasyon sonrası tek bir cümle."""

    index: int
    paragraph: int
    display: str
    spoken: str = ""
    is_dialogue: bool = False
    is_heading: bool = False


@dataclass(slots=True)
class Paragraph:
    """Paragraf içindeki cümleler bütünü."""

    index: int
    sentences: list[Sentence] = field(default_factory=list)


@dataclass(slots=True)
class Chunk:
    """TTS motoruna gönderilecek planlanmış metin parçası ve cümle ağırlık eşlemesi."""

    id: str
    text: str
    sentence_weights: list[tuple[int, float]] = field(default_factory=list)
    paragraph: int = 0
    is_paragraph_end: bool = False
    is_heading: bool = False
    is_dialogue_transition: bool = False
    is_subchunk: bool = False


@dataclass(slots=True)
class ChunkAudio:
    """Üretilmiş veya önbellekten yüklenmiş tek bir parçanın ses verisi."""

    chunk_id: str
    samples: np.ndarray
    sample_rate: int
    attempts: int = 1
    engine_id: str = ""
    from_cache: bool = False

    @property
    def duration_ms(self) -> int:
        if self.sample_rate <= 0:
            return 0
        return int(round(len(self.samples) * 1000.0 / self.sample_rate))


@dataclass(slots=True)
class SentenceTimestamp:
    """Bölüm içindeki tek bir cümlenin milisaniye cinsinden kesin zaman damgası (Bölüm 4.7.2)."""

    i: int
    p: int
    s: int
    e: int
    t: str

    def to_dict(self) -> dict[str, Any]:
        return {
            "i": self.i,
            "p": self.p,
            "s": self.s,
            "e": self.e,
            "t": self.t,
        }


@dataclass(slots=True)
class ChapterResult:
    """Tek bir bölümün tamamlanan seslendirme çıktısı."""

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


@dataclass(slots=True)
class JobStats:
    """Sentez işinin kümülatif istatistikleri."""

    completed: int = 0
    failed: int = 0
    total_gpu_ms: int = 0
    total_audio_sec: float = 0.0
    used_engines: list[str] = field(default_factory=list)

    def add(self, result: ChapterResult) -> None:
        self.completed += 1
        self.total_gpu_ms += result.gpu_ms
        self.total_audio_sec = round(self.total_audio_sec + result.sure_sn, 2)
        if result.motor and result.motor not in self.used_engines:
            self.used_engines.append(result.motor)
