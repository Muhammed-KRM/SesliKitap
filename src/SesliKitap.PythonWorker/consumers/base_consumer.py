from __future__ import annotations

import json
import time
from abc import ABC, abstractmethod
from typing import Any, ClassVar, Generic, TypeVar

import pika
from pydantic import BaseModel, ValidationError

from config import settings
from consumers.heartbeat_manager import RabbitHeartbeatKeeper
from models.events import ProcessingError
from services.pipeline_logger import PipelineLogger
from services.rabbitmq_publisher import RabbitMQPublisher
from utils.constants import Exchanges, Stage
from utils.logger import get_logger, set_current_trace_id

logger = get_logger(__name__)
TMessage = TypeVar("TMessage", bound=BaseModel)


class BaseConsumer(ABC, Generic[TMessage]):
    """Tüm RabbitMQ tüketicilerinin ortak bağlantı, DLQ deklarasyonu, ack/nack ve hata yönetimi taban sınıfı."""

    queue_name: ClassVar[str]
    exchange_names: ClassVar[list[str]]
    message_model: ClassVar[type[BaseModel]]
    default_stage: ClassVar[str] = Stage.METIN_CIKARMA

    def __init__(
        self,
        publisher: RabbitMQPublisher | None = None,
        pipeline_logger: PipelineLogger | None = None,
    ) -> None:
        self.publisher = publisher or RabbitMQPublisher()
        self.pipeline_logger = pipeline_logger or PipelineLogger()
        self._connection: pika.BlockingConnection | None = None
        self._channel: Any = None
        self._stop_requested = False

    def start(self) -> None:
        """RabbitMQ bağlantısını kurar, kuyruk/DLQ yapılandırmasını tamamlar ve mesaj dinlemeye başlar."""
        while not self._stop_requested:
            try:
                credentials = pika.PlainCredentials(
                    settings.rabbitmq_user,
                    settings.rabbitmq_password.get_secret_value(),
                )
                params = pika.ConnectionParameters(
                    host=settings.rabbitmq_host,
                    port=settings.rabbitmq_port,
                    virtual_host=settings.rabbitmq_vhost,
                    credentials=credentials,
                    heartbeat=0,
                    blocked_connection_timeout=0,
                )
                self._connection = pika.BlockingConnection(params)
                self._channel = self._connection.channel()

                dlq_name = f"{self.queue_name}.dlq"
                self._channel.exchange_declare(
                    exchange=Exchanges.DLX,
                    exchange_type="direct",
                    durable=True,
                )
                self._channel.queue_declare(queue=dlq_name, durable=True)
                self._channel.queue_bind(
                    queue=dlq_name,
                    exchange=Exchanges.DLX,
                    routing_key=dlq_name,
                )

                self._channel.queue_declare(
                    queue=self.queue_name,
                    durable=True,
                    arguments={
                        "x-dead-letter-exchange": Exchanges.DLX,
                        "x-dead-letter-routing-key": dlq_name,
                    },
                )

                for ex_name in self.exchange_names:
                    self._channel.exchange_declare(
                        exchange=ex_name,
                        exchange_type="fanout",
                        durable=True,
                    )
                    self._channel.queue_bind(queue=self.queue_name, exchange=ex_name)

                self._channel.basic_qos(prefetch_count=1)
                self._channel.basic_consume(
                    queue=self.queue_name,
                    on_message_callback=self._on_message,
                    auto_ack=False,
                )

                logger.info("%s başlatıldı, kuyruk dinleniyor: %s", self.__class__.__name__, self.queue_name)
                self._channel.start_consuming()
            except Exception as exc:
                if self._stop_requested:
                    break
                logger.error(
                    "%s bağlantı hatası (%s), 5 sn sonra yeniden bağlanılacak...",
                    self.__class__.__name__,
                    exc,
                )
                time.sleep(5.0)

    def stop(self) -> None:
        """Tüketiciyi nazikçe durdurur."""
        self._stop_requested = True
        try:
            if self._channel and getattr(self._channel, "is_open", False):
                self._channel.stop_consuming()
            if self._connection and getattr(self._connection, "is_open", False):
                self._connection.close()
        except Exception:
            pass

    def _on_message(self, ch: Any, method: Any, properties: Any, body: bytes) -> None:
        delivery_tag = getattr(method, "delivery_tag", None)
        try:
            raw_obj = json.loads(body.decode("utf-8"))
            if isinstance(raw_obj, dict) and "message" in raw_obj and isinstance(raw_obj["message"], dict):
                raw_obj = raw_obj["message"]
            message: TMessage = self.message_model.model_validate(raw_obj)  # type: ignore[assignment]
        except (UnicodeDecodeError, json.JSONDecodeError, ValidationError) as parse_err:
            logger.error(
                "[%s] Geçersiz mesaj şeması, DLQ'ya yönlendiriliyor: %s",
                self.queue_name,
                parse_err,
            )
            if ch is not None and delivery_tag is not None:
                ch.basic_nack(delivery_tag=delivery_tag, requeue=False)
            return

        trace_id = getattr(message, "trace_id", None)
        set_current_trace_id(trace_id)
        book_id = getattr(message, "book_id", None)
        job_id = getattr(message, "job_id", None)
        voice_profile_id = getattr(message, "voice_profile_id", None)

        heartbeat = RabbitHeartbeatKeeper(self._connection)
        heartbeat.start()
        try:
            self.handle(message)
            if ch is not None and delivery_tag is not None:
                ch.basic_ack(delivery_tag=delivery_tag)
        except ProcessingError as proc_err:
            logger.warning(
                "[%s] Beklenen işleme hatası (%s): %s",
                self.queue_name,
                proc_err.code,
                proc_err.message,
            )
            self._safe_publish_error(
                book_id=book_id,
                job_id=job_id,
                voice_profile_id=voice_profile_id,
                asama=self.default_stage,
                hata_kodu=proc_err.code,
                hata_mesaji=proc_err.message,
                kurtarilabilir=proc_err.recoverable,
                trace_id=trace_id,
            )
            if ch is not None and delivery_tag is not None:
                ch.basic_ack(delivery_tag=delivery_tag)
        except Exception as exc:
            logger.exception("[%s] Beklenmeyen işleme hatası: %s", self.queue_name, exc)
            self._safe_publish_error(
                book_id=book_id,
                job_id=job_id,
                voice_profile_id=voice_profile_id,
                asama=self.default_stage,
                hata_kodu="WORKER_UNEXPECTED_ERROR",
                hata_mesaji=f"İşleme sırasında beklenmeyen bir hata oluştu: {exc}",
                kurtarilabilir=True,
                trace_id=trace_id,
            )
            if ch is not None and delivery_tag is not None:
                ch.basic_nack(delivery_tag=delivery_tag, requeue=False)
        finally:
            heartbeat.stop()
            set_current_trace_id(None)

    def _safe_publish_error(
        self,
        *,
        book_id: str | None,
        job_id: str | None,
        voice_profile_id: str | None,
        asama: str,
        hata_kodu: str,
        hata_mesaji: str,
        kurtarilabilir: bool,
        trace_id: str | None,
    ) -> None:
        try:
            self.publisher.publish_pipeline_error(
                book_id=book_id,
                job_id=job_id,
                voice_profile_id=voice_profile_id,
                asama=asama,
                hata_kodu=hata_kodu,
                hata_mesaji=hata_mesaji,
                kurtarilabilir=kurtarilabilir,
                trace_id=trace_id,
            )
        except Exception as pub_err:
            logger.error("PipelineErrorEvent yayınlanamadı: %s", pub_err)

        self.pipeline_logger.log_event(
            asama=asama,
            durum="Hata",
            book_id=book_id,
            job_id=job_id,
            mesaj=hata_mesaji,
            hata_mesaji=f"{hata_kodu}: {hata_mesaji}",
            trace_id=trace_id,
        )

    @abstractmethod
    def handle(self, message: TMessage) -> None:
        """Alt sınıf tarafından uygulanan mesaj işleme mantığı."""
