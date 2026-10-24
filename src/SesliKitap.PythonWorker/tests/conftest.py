from __future__ import annotations

import json
from pathlib import Path
from typing import Any, TypeVar

import pytest
from pydantic import BaseModel

from config import settings
from models.events import (
    BookExportCompletedEvent,
    BookProcessedEvent,
    BookSourceStoredEvent,
    ChapterSynthesizedEvent,
    IntegrationEventBase,
    PipelineErrorEvent,
    PipelineStageChangedEvent,
    SynthesisCompletedEvent,
)

T = TypeVar("T", bound=BaseModel)


class FakeMinioClient:
    """Bellek içi sözlük tabanlı sahte MinIO istemcisi."""

    def __init__(self) -> None:
        self.objects: dict[str, bytes] = {}
        self.content_types: dict[str, str] = {}

    def ensure_bucket(self) -> None:
        pass

    def upload_file(self, key: str, path: Path, content_type: str = "application/octet-stream") -> None:
        self.objects[key] = path.read_bytes()
        self.content_types[key] = content_type

    def download_file(self, key: str, path: Path) -> Path:
        if key not in self.objects:
            raise FileNotFoundError(f"MinIO key not found: {key}")
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(self.objects[key])
        return path

    def upload_bytes(self, key: str, data: bytes, content_type: str = "application/octet-stream") -> None:
        self.objects[key] = data
        self.content_types[key] = content_type

    def download_bytes(self, key: str) -> bytes:
        if key not in self.objects:
            raise FileNotFoundError(f"MinIO key not found: {key}")
        return self.objects[key]

    def write_json(self, key: str, payload: BaseModel | dict[str, Any]) -> None:
        raw_dict = payload.model_dump(by_alias=True, mode="json") if isinstance(payload, BaseModel) else payload
        data = json.dumps(raw_dict, ensure_ascii=False, indent=2).encode("utf-8")
        self.upload_bytes(key, data, content_type="application/json; charset=utf-8")

    def read_json(self, key: str, model: type[T] | None = None) -> T | dict[str, Any]:
        raw = self.download_bytes(key).decode("utf-8")
        if model is not None:
            return model.model_validate_json(raw)
        return json.loads(raw)

    def exists(self, key: str) -> bool:
        return key in self.objects


class FakeRabbitMQPublisher:
    """Yayınlanan mesajları bellek içi listelerde toplayan sahte RabbitMQ yayıncısı."""

    def __init__(self) -> None:
        self.stage_events: list[PipelineStageChangedEvent] = []
        self.source_stored_events: list[BookSourceStoredEvent] = []
        self.processed_events: list[BookProcessedEvent] = []
        self.error_events: list[PipelineErrorEvent] = []
        self.chapter_synthesized_events: list[ChapterSynthesizedEvent] = []
        self.synthesis_completed_events: list[SynthesisCompletedEvent] = []
        self.export_completed_events: list[BookExportCompletedEvent] = []
        self.raw_events: list[tuple[str, Any]] = []

    def publish_raw(self, exchange_short_name: str, payload: dict[str, Any] | IntegrationEventBase) -> None:
        self.raw_events.append((exchange_short_name, payload))

    def publish_book_source_stored(self, event: BookSourceStoredEvent) -> None:
        self.source_stored_events.append(event)
        self.publish_raw("BookSourceStoredEvent", event)

    def publish_book_processed(self, event: BookProcessedEvent) -> None:
        self.processed_events.append(event)

    def publish_chapter_synthesized(self, event: ChapterSynthesizedEvent) -> None:
        self.chapter_synthesized_events.append(event)

    def publish_synthesis_completed(self, event: SynthesisCompletedEvent) -> None:
        self.synthesis_completed_events.append(event)

    def publish_book_export_completed(self, event: BookExportCompletedEvent) -> None:
        self.export_completed_events.append(event)
        self.publish_raw("BookExportCompletedEvent", event)

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
        self.stage_events.append(
            PipelineStageChangedEvent(
                trace_id=trace_id,
                book_id=book_id,
                job_id=job_id,
                chapter_id=chapter_id,
                asama=asama,
                yuzde=yuzde,
                mesaj=mesaj,
                detay=detay,
            )
        )

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
        self.error_events.append(
            PipelineErrorEvent(
                trace_id=trace_id,
                book_id=book_id,
                job_id=job_id,
                chapter_id=chapter_id,
                voice_profile_id=voice_profile_id,
                asama=asama,
                hata_kodu=hata_kodu,
                hata_mesaji=hata_mesaji,
                kurtarilabilir=kurtarilabilir,
            )
        )


class FakePipelineLogger:
    """Pipeline log kayıtlarını bellekte toplayan sahte logger."""

    def __init__(self) -> None:
        self.logs: list[dict[str, Any]] = []

    def log_event(self, **kwargs: Any) -> None:
        self.logs.append(kwargs)


@pytest.fixture(autouse=True)
def _isolated_temp_dir(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(settings, "temp_dir", tmp_path / "worker_temp")
    (tmp_path / "worker_temp").mkdir(parents=True, exist_ok=True)


@pytest.fixture
def fake_minio() -> FakeMinioClient:
    return FakeMinioClient()


@pytest.fixture
def fake_publisher() -> FakeRabbitMQPublisher:
    return FakeRabbitMQPublisher()


class FakeRedis:
    """Bellek içi sözlük tabanlı sahte Redis istemcisi."""

    def __init__(self) -> None:
        self.store: dict[str, str] = {}

    def get(self, key: str) -> str | None:
        return self.store.get(key)

    def set(self, key: str, value: str) -> None:
        self.store[key] = str(value)

    def setex(self, key: str, ttl: int, value: str) -> None:
        self.store[key] = str(value)

    def mget(self, keys: list[str]) -> list[str | None]:
        return [self.store.get(k) for k in keys]


@pytest.fixture
def fake_redis() -> FakeRedis:
    return FakeRedis()


@pytest.fixture
def fake_pipeline_logger() -> FakePipelineLogger:
    return FakePipelineLogger()
