"""Ses profili hazırlama ve örnek ses üretimi RabbitMQ tüketicisi (Bölüm 4.15.6)."""

from __future__ import annotations

from collections.abc import Callable
from pathlib import Path
from typing import ClassVar

import soundfile as sf

from config import settings
from consumers.base_consumer import BaseConsumer
from models.events import (
    ProcessingError,
    VoiceProfileReadyEvent,
    VoiceProfileUploadedEvent,
)
from models.voice_models import PreparedVoice, SynthChunkInput, VoiceParams
from pipeline.audio.encoder import Encoder
from pipeline.audio.reference_preparer import ReferencePreparer
from pipeline.tts.base_engine import EngineUnavailableError, FamilyExhaustedError
from pipeline.tts.engine_chain import TtsEngineChain, build_default_engine_chain
from services.minio_client import MinioStorageClient
from services.pipeline_logger import PipelineLogger
from services.rabbitmq_publisher import RabbitMQPublisher
from utils.constants import Exchanges, Queues, Stage, StoragePaths
from utils.janitor import cleanup_job
from utils.logger import get_logger

logger = get_logger(__name__)


class VoiceProfileConsumer(BaseConsumer[VoiceProfileUploadedEvent]):
    """VoiceProfileUploadedEvent mesajlarını dinler, referans WAV ve örnek Opus üretir."""

    queue_name: ClassVar[str] = Queues.VOICE_PROFILE
    exchange_names: ClassVar[list[str]] = [
        Exchanges.VOICE_PROFILE_UPLOADED,
        f"{Exchanges.PREFIX}{Exchanges.VOICE_PROFILE_UPLOADED}",
    ]
    message_model: ClassVar[type[VoiceProfileUploadedEvent]] = VoiceProfileUploadedEvent
    default_stage: ClassVar[str] = Stage.REFERANS_HAZIRLAMA

    def __init__(
        self,
        publisher: RabbitMQPublisher | None = None,
        pipeline_logger: PipelineLogger | None = None,
        minio_client: MinioStorageClient | None = None,
        preparer: ReferencePreparer | None = None,
        encoder: Encoder | None = None,
        chain_factory: Callable[[], TtsEngineChain] | None = None,
    ) -> None:
        super().__init__(publisher=publisher, pipeline_logger=pipeline_logger)
        self.minio = minio_client or MinioStorageClient()
        self.preparer = preparer or ReferencePreparer()
        self.encoder = encoder or Encoder()
        self.chain_factory = chain_factory or (lambda: build_default_engine_chain())

    def handle(self, message: VoiceProfileUploadedEvent) -> None:
        voice_id = message.voice_profile_id
        trace_id = message.trace_id
        job_dir = settings.temp_dir / f"voice-{voice_id}"
        job_dir.mkdir(parents=True, exist_ok=True)
        gpu_ms = 0

        try:
            self.pipeline_logger.log_event(
                asama=Stage.REFERANS_HAZIRLAMA,
                durum="Basladi",
                yuzde=10,
                mesaj="Referans ses kaydı indiriliyor ve temizleniyor...",
                trace_id=trace_id,
            )

            ext = Path(message.ham_ses_yolu).suffix or ".wav"
            raw_local = job_dir / f"raw{ext}"
            self.minio.download_file(message.ham_ses_yolu, raw_local)

            ref_local = job_dir / "reference.wav"
            prepared = self.preparer.prepare(raw_local, out_wav_path=ref_local)

            ref_key = StoragePaths.voice_reference(voice_id)
            self.minio.upload_file(ref_key, prepared.wav_path, content_type="audio/wav")

            self.pipeline_logger.log_event(
                asama=Stage.SENTEZ,
                durum="Devam",
                yuzde=50,
                mesaj="Örnek Türkçe paragraf seslendiriliyor...",
                trace_id=trace_id,
            )

            chain = self.chain_factory()
            chain.pin_for_chapter()
            voice = PreparedVoice(
                voice_id=voice_id,
                voice_hash=prepared.sha256,
                wav_path=prepared.wav_path,
                prompt_text=message.referans_metin,
                duration_sec=prepared.duration_sec,
            )
            params = VoiceParams()
            chunk = SynthChunkInput(
                id=f"vp-{voice_id[:8]}-sample",
                text=message.ornek_metin.strip(),
            )
            batch_res = chain.synthesize(
                [chunk],
                voice,
                params,
                request_id=f"voice-sample-{voice_id[:8]}",
                language="tr",
            )
            gpu_ms = batch_res.gpu_ms
            if not batch_res.outputs:
                raise ProcessingError(
                    "VOICE_SAMPLE_SYNTH_FAILED",
                    "Örnek ses üretimi sırasında ses verisi alınamadı.",
                    recoverable=True,
                )

            out_audio = batch_res.outputs[0]
            sample_wav = job_dir / "sample.wav"
            sf.write(
                str(sample_wav),
                out_audio.samples,
                out_audio.sample_rate,
                format="WAV",
                subtype="PCM_16",
            )

            sample_ogg = job_dir / "sample.ogg"
            self.encoder.to_opus(sample_wav, sample_ogg, bitrate_kbps=48)

            sample_key = StoragePaths.voice_sample(voice_id)
            self.minio.upload_file(sample_key, sample_ogg, content_type="audio/ogg")

            ready_evt = VoiceProfileReadyEvent(
                trace_id=trace_id,
                voice_profile_id=voice_id,
                basarili=True,
                referans_ses_yolu=ref_key,
                referans_hash=prepared.sha256,
                referans_sure_sn=prepared.duration_sec,
                ornek_ses_yolu=sample_key,
                gpu_ms=gpu_ms,
                hata_mesaji=None,
            )
            self.publisher.publish_voice_profile_ready(ready_evt)

            self.pipeline_logger.log_event(
                asama=Stage.TAMAMLANDI,
                durum="Tamamlandi",
                yuzde=100,
                mesaj="Ses profili başarıyla hazırlandı.",
                trace_id=trace_id,
            )
        except (ProcessingError, EngineUnavailableError, FamilyExhaustedError) as err:
            err_msg = err.message if isinstance(err, ProcessingError) else str(err)
            err_code = err.code if isinstance(err, ProcessingError) else "TTS_ENGINE_UNAVAILABLE"
            logger.warning("Ses profili hazırlama hatası (voiceId=%s): %s", voice_id, err_msg)

            self.publisher.publish_voice_profile_ready(
                VoiceProfileReadyEvent(
                    trace_id=trace_id,
                    voice_profile_id=voice_id,
                    basarili=False,
                    gpu_ms=gpu_ms,
                    hata_mesaji=err_msg,
                )
            )
            self._safe_publish_error(
                book_id=None,
                job_id=None,
                voice_profile_id=voice_id,
                asama=Stage.REFERANS_HAZIRLAMA,
                hata_kodu=err_code,
                hata_mesaji=err_msg,
                kurtarilabilir=False,
                trace_id=trace_id,
            )
        finally:
            cleanup_job(job_dir)
