from __future__ import annotations

import io
import json
from pathlib import Path
from typing import Any, TypeVar

from minio import Minio
from minio.error import S3Error
from pydantic import BaseModel
from tenacity import retry, stop_after_attempt, wait_exponential

from config import settings
from utils.logger import get_logger

logger = get_logger(__name__)
T = TypeVar("T", bound=BaseModel)


class MinioStorageClient:
    """MinIO nesne depolama istemcisi (otomatik yeniden deneme destekli)."""

    def __init__(self, client: Minio | None = None, bucket: str | None = None) -> None:
        self._bucket = bucket or settings.minio_bucket
        self._client = client or Minio(
            endpoint=settings.minio_endpoint,
            access_key=settings.minio_access_key,
            secret_key=settings.minio_secret_key.get_secret_value(),
            secure=settings.minio_secure,
        )

    @retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=0.5, min=0.5, max=5), reraise=True)
    def ensure_bucket(self) -> None:
        """Bucket yoksa oluşturur."""
        if not self._client.bucket_exists(self._bucket):
            self._client.make_bucket(self._bucket)

    @retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=0.5, min=0.5, max=5), reraise=True)
    def upload_file(self, key: str, path: Path, content_type: str = "application/octet-stream") -> None:
        """Yerel dosyayı MinIO'ya yükler."""
        self._client.fput_object(self._bucket, key, str(path), content_type=content_type)
        logger.debug("MinIO dosya yüklendi: %s (%d bayt)", key, path.stat().st_size)

    @retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=0.5, min=0.5, max=5), reraise=True)
    def download_file(self, key: str, path: Path) -> Path:
        """MinIO nesnesini yerel dosyaya indirir."""
        path.parent.mkdir(parents=True, exist_ok=True)
        self._client.fget_object(self._bucket, key, str(path))
        logger.debug("MinIO dosya indirildi: %s -> %s", key, path)
        return path

    @retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=0.5, min=0.5, max=5), reraise=True)
    def upload_bytes(self, key: str, data: bytes, content_type: str = "application/octet-stream") -> None:
        """Bayt dizisini MinIO'ya yükler."""
        stream = io.BytesIO(data)
        self._client.put_object(
            self._bucket,
            key,
            stream,
            length=len(data),
            content_type=content_type,
        )
        logger.debug("MinIO bayt yüklendi: %s (%d bayt)", key, len(data))

    @retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=0.5, min=0.5, max=5), reraise=True)
    def download_bytes(self, key: str) -> bytes:
        """MinIO nesnesini bellek içi bayt dizisi olarak okur."""
        response = self._client.get_object(self._bucket, key)
        try:
            return response.read()
        finally:
            response.close()
            response.release_conn()

    def write_json(self, key: str, payload: BaseModel | dict[str, Any]) -> None:
        """Pydantic modelini veya sözlüğü UTF-8 JSON olarak MinIO'ya yazar."""
        if isinstance(payload, BaseModel):
            raw_dict = payload.model_dump(by_alias=True, mode="json")
        else:
            raw_dict = payload
        data = json.dumps(raw_dict, ensure_ascii=False, indent=2).encode("utf-8")
        self.upload_bytes(key, data, content_type="application/json; charset=utf-8")

    def read_json(self, key: str, model: type[T] | None = None) -> T | dict[str, Any]:
        """MinIO'daki JSON nesnesini okur ve istenirse Pydantic modeline dönüştürür."""
        raw = self.download_bytes(key).decode("utf-8")
        if model is not None:
            return model.model_validate_json(raw)
        return json.loads(raw)

    def exists(self, key: str) -> bool:
        """MinIO'da nesnenin var olup olmadığını kontrol eder."""
        try:
            self._client.stat_object(self._bucket, key)
            return True
        except S3Error as err:
            if err.code in ("NoSuchKey", "NoSuchObject"):
                return False
            raise
