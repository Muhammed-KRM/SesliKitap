"""M4B sesli kitap dışa aktarım isteklerini (BookExportRequestedEvent) işleyen tüketici (Faz 8 — Adım 8.3)."""

from __future__ import annotations

from consumers.base_consumer import BaseConsumer
from models.events import (
    BookExportCompletedEvent,
    BookExportRequestedEvent,
    ProcessingError,
)
from pipeline.audio.m4b_builder import M4bBuilder, M4bChapterInput
from services.minio_client import MinioStorageClient
from services.pipeline_logger import PipelineLogger
from services.rabbitmq_publisher import RabbitMQPublisher
from utils.constants import Exchanges, Queues, Stage
from utils.janitor import cleanup_job, create_job_temp_dir
from utils.logger import get_logger

logger = get_logger(__name__)


class ExportConsumer(BaseConsumer[BookExportRequestedEvent]):
    """Kitabın hazır bölümlerini indirip M4B olarak birleştiren ve MinIO'ya yükleyen tüketici."""

    queue_name = Queues.EXPORT
    exchange_names = [
        Exchanges.BOOK_EXPORT_REQUESTED,
        f"{Exchanges.PREFIX}{Exchanges.BOOK_EXPORT_REQUESTED}",
    ]
    message_model = BookExportRequestedEvent
    default_stage = Stage.PAKETLEME

    def __init__(
        self,
        minio_client: MinioStorageClient | None = None,
        publisher: RabbitMQPublisher | None = None,
        pipeline_logger: PipelineLogger | None = None,
        m4b_builder: M4bBuilder | None = None,
    ) -> None:
        super().__init__(publisher=publisher, pipeline_logger=pipeline_logger)
        self.minio = minio_client or MinioStorageClient()
        self.builder = m4b_builder or M4bBuilder()

    def handle(self, message: BookExportRequestedEvent) -> None:
        book_id = message.book_id
        trace_id = message.trace_id
        job_dir = create_job_temp_dir(f"export_{book_id}")

        logger.info(
            "M4B dışa aktarım başladı: bookId=%s, bolumSayisi=%d",
            book_id,
            len(message.bolumler),
        )

        try:
            if not message.bolumler:
                raise ProcessingError(
                    "EXPORT_NO_CHAPTERS",
                    "Dışa aktarılacak hazır bölüm bulunamadı.",
                    recoverable=False,
                )

            chapter_inputs: list[M4bChapterInput] = []
            for ch in sorted(message.bolumler, key=lambda x: x.sira_no):
                ext = ch.ses_yolu.rsplit(".", 1)[-1].lower() if "." in ch.ses_yolu else "ogg"
                local_audio = job_dir / f"ch_{ch.sira_no:04d}.{ext}"
                self.minio.download_file(ch.ses_yolu, local_audio)
                chapter_inputs.append(
                    M4bChapterInput(
                        sira_no=ch.sira_no,
                        baslik=ch.baslik,
                        audio_path=local_audio,
                        duration_sec=ch.sure_sn,
                    )
                )

            cover_path = None
            if message.kapak_yolu and self.minio.exists(message.kapak_yolu):
                candidate_cover = job_dir / "cover.jpg"
                self.minio.download_file(message.kapak_yolu, candidate_cover)
                if candidate_cover.is_file() and candidate_cover.stat().st_size > 0:
                    cover_path = candidate_cover

            out_m4b = job_dir / "audiobook.m4b"
            size_bytes = self.builder.build(
                chapter_inputs,
                out_m4b,
                book_title=message.baslik,
                author=message.yazar,
                cover_path=cover_path,
                work_dir=job_dir,
            )

            self.minio.upload_file(message.cikti_yolu, out_m4b, content_type="audio/mp4")

            self.publisher.publish_book_export_completed(
                BookExportCompletedEvent(
                    trace_id=trace_id,
                    book_id=book_id,
                    format="m4b",
                    basarili=True,
                    dosya_yolu=message.cikti_yolu,
                    boyut=size_bytes,
                )
            )
            logger.info(
                "M4B dışa aktarım tamamlandı: bookId=%s, key=%s, boyut=%d",
                book_id,
                message.cikti_yolu,
                size_bytes,
            )
        except Exception as exc:
            logger.error("M4B dışa aktarım hatası (bookId=%s): %s", book_id, exc)
            self.publisher.publish_book_export_completed(
                BookExportCompletedEvent(
                    trace_id=trace_id,
                    book_id=book_id,
                    format="m4b",
                    basarili=False,
                    dosya_yolu=None,
                    boyut=0,
                    hata_mesaji=str(exc),
                )
            )
            raise
        finally:
            cleanup_job(job_dir)
