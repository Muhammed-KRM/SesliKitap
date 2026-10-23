from __future__ import annotations

from pathlib import Path
from typing import Annotated, Literal

from pydantic import AliasChoices, Field, SecretStr, field_validator
from pydantic_settings import BaseSettings, NoDecode, SettingsConfigDict


def _find_env_file() -> Path | None:
    """Çalışma dizininden yukarı doğru .env dosyasını arar."""
    current = Path.cwd().resolve()
    for directory in [current, *current.parents]:
        candidate = directory / ".env"
        if candidate.is_file():
            return candidate
    return None


class Config(BaseSettings):
    """Worker yapılandırma ayarları (.env + ortam değişkenleri)."""

    # RabbitMQ
    rabbitmq_host: str = Field(default="localhost", validation_alias=AliasChoices("RABBITMQ_HOST", "rabbitmq_host"))
    rabbitmq_port: int = Field(default=5673, validation_alias=AliasChoices("RABBITMQ_PORT", "rabbitmq_port"))
    rabbitmq_user: str = Field(
        default="guest",
        validation_alias=AliasChoices("RABBITMQ_USER", "RABBITMQ_USERNAME", "rabbitmq_user"),
    )
    rabbitmq_password: SecretStr = Field(
        default=SecretStr("guest"),
        validation_alias=AliasChoices("RABBITMQ_PASSWORD", "rabbitmq_password"),
    )
    rabbitmq_vhost: str = Field(default="/", validation_alias=AliasChoices("RABBITMQ_VHOST", "rabbitmq_vhost"))

    # MinIO
    minio_endpoint: str = Field(default="localhost:9010", validation_alias=AliasChoices("MINIO_ENDPOINT", "minio_endpoint"))
    minio_access_key: str = Field(
        default="minioadmin",
        validation_alias=AliasChoices("MINIO_ACCESS_KEY", "MINIO_ROOT_USER", "minio_access_key"),
    )
    minio_secret_key: SecretStr = Field(
        default=SecretStr("minioadmin"),
        validation_alias=AliasChoices("MINIO_SECRET_KEY", "MINIO_ROOT_PASSWORD", "minio_secret_key"),
    )
    minio_bucket: str = Field(
        default="seslikitap",
        validation_alias=AliasChoices("MINIO_BUCKET_NAME", "MINIO_BUCKET", "minio_bucket"),
    )
    minio_secure: bool = Field(
        default=False,
        validation_alias=AliasChoices("MINIO_USE_SSL", "MINIO_SECURE", "minio_secure"),
    )

    # Redis
    redis_url: str = Field(
        default="redis://localhost:6380/0",
        validation_alias=AliasChoices("REDIS_URL", "REDIS_CONNECTION_STRING", "redis_url"),
    )

    # API & Genel
    api_base_url: str = Field(
        default="http://localhost:5201",
        validation_alias=AliasChoices("API_BASE_URL", "api_base_url"),
    )
    internal_api_key: SecretStr = Field(
        default=SecretStr("dev-secret-key"),
        validation_alias=AliasChoices("ADMIN_API_KEY", "INTERNAL_API_KEY", "internal_api_key"),
    )
    temp_dir: Path = Field(
        default=Path("./temp"),
        validation_alias=AliasChoices("WORKER_TEMP_DIR", "TEMP_DIR", "temp_dir"),
    )
    logs_dir: Path = Field(default=Path("./logs"))
    temp_retention: Literal["after_job", "keep"] = Field(
        default="after_job",
        validation_alias=AliasChoices("TEMP_RETENTION", "temp_retention"),
    )
    log_level: str = Field(default="INFO", validation_alias=AliasChoices("LOG_LEVEL", "log_level"))

    # TTS / GPU
    tts_engine_order: Annotated[list[str], NoDecode] = Field(
        default=["voxcpm2_modal"],
        validation_alias=AliasChoices("TTS_ENGINE_ORDER", "tts_engine_order"),
    )
    tts_modal_url: str | None = Field(default=None, validation_alias=AliasChoices("TTS_MODAL_URL", "tts_modal_url"))
    tts_alt_url: str | None = Field(default=None, validation_alias=AliasChoices("TTS_ALT_URL", "tts_alt_url"))
    tts_xtts_url: str | None = Field(default=None, validation_alias=AliasChoices("TTS_XTTS_URL", "tts_xtts_url"))
    tts_api_token: SecretStr | None = Field(default=None, validation_alias=AliasChoices("TTS_API_TOKEN", "tts_api_token"))
    tts_batch_size: int = Field(default=16, validation_alias=AliasChoices("TTS_BATCH_SIZE", "tts_batch_size"))
    tts_max_concurrent_requests: int = Field(
        default=2,
        validation_alias=AliasChoices("TTS_MAX_CONCURRENT_REQUESTS", "tts_max_concurrent_requests"),
    )
    tts_request_timeout_sec: int = Field(
        default=600,
        validation_alias=AliasChoices("TTS_REQUEST_TIMEOUT_SEC", "tts_request_timeout_sec"),
    )
    tts_cold_start_timeout_sec: int = Field(
        default=300,
        validation_alias=AliasChoices("TTS_COLD_START_TIMEOUT_SEC", "tts_cold_start_timeout_sec"),
    )
    tts_max_retries: int = Field(default=3, validation_alias=AliasChoices("TTS_MAX_RETRIES", "tts_max_retries"))
    tts_max_chunk_chars: int = Field(default=280, validation_alias=AliasChoices("TTS_MAX_CHUNK_CHARS", "tts_max_chunk_chars"))
    tts_min_chunk_chars: int = Field(default=25, validation_alias=AliasChoices("TTS_MIN_CHUNK_CHARS", "tts_min_chunk_chars"))
    tts_transfer_sample_rate: int = 24000
    output_sample_rate: int = 48000
    max_download_bytes: int = 300 * 1024 * 1024

    model_config = SettingsConfigDict(
        env_file=_find_env_file(),
        env_file_encoding="utf-8",
        extra="ignore",
    )

    @field_validator("tts_engine_order", mode="before")
    @classmethod
    def _split_engines(cls, value: str | list[str]) -> list[str]:
        if isinstance(value, str):
            return [s.strip() for s in value.split(",") if s.strip()]
        return value

    @field_validator("redis_url", mode="before")
    @classmethod
    def _normalize_redis_url(cls, value: str) -> str:
        if isinstance(value, str) and not value.startswith(("redis://", "rediss://")):
            return f"redis://{value}/0"
        return value


settings = Config()


def ensure_directories() -> None:
    """Geçici çalışma ve log dizinlerinin varlığını garanti eder."""
    settings.temp_dir.mkdir(parents=True, exist_ok=True)
    settings.logs_dir.mkdir(parents=True, exist_ok=True)
