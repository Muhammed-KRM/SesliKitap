from __future__ import annotations

import json
import threading
from typing import Any

import pika

from config import settings
from models.events import (
    BookProcessedEvent,
    IntegrationEventBase,
    PipelineErrorEvent,
    PipelineStageChangedEvent,
    VoiceProfileReadyEvent,
)
from utils.constants import Exchanges
from utils.logger import get_current_trace_id, get_logger

logger = get_logger(__name__)


class RabbitMQPublisher:
    """Python Worker'dan .NET MassTransit tüketicilerine raw JSON event yayınlayan thread-safe yayıncı."""

    def __init__(self) -> None:
        self._connection: pika.BlockingConnection | None = None
        self._channel: pika.adapters.blocking_connection.BlockingChannel | None = None
        self._lock = threading.Lock()

    def _create_connection(self) -> pika.BlockingConnection:
        credentials = pika.PlainCredentials(
            settings.rabbitmq_user,
            settings.rabbitmq_password.get_secret_value(),
        )
        params = pika.ConnectionParameters(
            host=settings.rabbitmq_host,
            port=settings.rabbitmq_port,
            virtual_host=settings.rabbitmq_vhost,
            credentials=credentials,
            heartbeat=60,
            blocked_connection_timeout=300,
        )
        return pika.BlockingConnection(params)

    def _get_channel(self) -> pika.adapters.blocking_connection.BlockingChannel:
        if self._connection is None or self._connection.is_closed:
            self._connection = self._create_connection()
            self._channel = self._connection.channel()
        elif self._channel is None or self._channel.is_closed:
            self._channel = self._connection.channel()
        return self._channel

    def publish_raw(self, exchange_short_name: str, payload: dict[str, Any] | IntegrationEventBase) -> None:
        """Verilen exchange'e (kısa ad + MassTransit tam ad) raw JSON yayınlar."""
        if isinstance(payload, IntegrationEventBase):
            if not payload.trace_id:
                trace = get_current_trace_id()
                if trace != "-":
                    payload.trace_id = trace
            body_dict = payload.model_dump(by_alias=True, mode="json")
        else:
            body_dict = dict(payload)

        body = json.dumps(body_dict, ensure_ascii=False).encode("utf-8")
        exchanges = [exchange_short_name]
        if not exchange_short_name.startswith(Exchanges.PREFIX):
            exchanges.append(f"{Exchanges.PREFIX}{exchange_short_name}")

        with self._lock:
            for attempt in range(2):
                try:
                    channel = self._get_channel()
                    props = pika.BasicProperties(
                        content_type="application/json",
                        delivery_mode=2,
                    )
                    for ex in exchanges:
                        channel.exchange_declare(exchange=ex, exchange_type="fanout", durable=True)
                        channel.basic_publish(exchange=ex, routing_key="", body=body, properties=props)
                    logger.info("RabbitMQ event yayınlandı: %s", exchange_short_name)
                    return
                except Exception as exc:
                    logger.warning(
                        "RabbitMQ yayın hatası (%s, deneme %d): %s",
                        exchange_short_name,
                        attempt + 1,
                        exc,
                    )
                    self._close()
                    if attempt == 1:
                        raise

    def publish_book_processed(self, event: BookProcessedEvent) -> None:
        """BookProcessedEvent yayınlar."""
        self.publish_raw(Exchanges.BOOK_PROCESSED, event)

    def publish_voice_profile_ready(self, event: VoiceProfileReadyEvent) -> None:
        """VoiceProfileReadyEvent yayınlar."""
        self.publish_raw(Exchanges.VOICE_PROFILE_READY, event)

    def publish_stage_changed(
        self,
        *,
        book_id: str | None = None,
        job_id: str | None = None,
        chapter_id: str | None = None,
        asama: str,
        yuzde: int,
        mesaj: str,
        detay: dict[str, Any] | None = None,
        trace_id: str | None = None,
    ) -> None:
        """PipelineStageChangedEvent yayınlar."""
        evt = PipelineStageChangedEvent(
            trace_id=trace_id or (get_current_trace_id() if get_current_trace_id() != "-" else None),
            book_id=book_id,
            job_id=job_id,
            chapter_id=chapter_id,
            asama=asama,
            yuzde=max(0, min(100, yuzde)),
            mesaj=mesaj,
            detay=detay,
        )
        self.publish_raw(Exchanges.STAGE_CHANGED, evt)

    def publish_pipeline_error(
        self,
        *,
        book_id: str | None = None,
        job_id: str | None = None,
        chapter_id: str | None = None,
        voice_profile_id: str | None = None,
        asama: str,
        hata_kodu: str,
        hata_mesaji: str,
        kurtarilabilir: bool = False,
        trace_id: str | None = None,
    ) -> None:
        """PipelineErrorEvent yayınlar."""
        evt = PipelineErrorEvent(
            trace_id=trace_id or (get_current_trace_id() if get_current_trace_id() != "-" else None),
            book_id=book_id,
            job_id=job_id,
            chapter_id=chapter_id,
            voice_profile_id=voice_profile_id,
            asama=asama,
            hata_kodu=hata_kodu,
            hata_mesaji=hata_mesaji,
            kurtarilabilir=kurtarilabilir,
        )
        self.publish_raw(Exchanges.PIPELINE_ERROR, evt)

    def _close(self) -> None:
        try:
            if self._channel and not self._channel.is_closed:
                self._channel.close()
            if self._connection and not self._connection.is_closed:
                self._connection.close()
        except Exception:
            pass
        self._channel = None
        self._connection = None
