"""GPU TTS sunucusu ortam değişkeni yapılandırması."""

from __future__ import annotations

from pathlib import Path
from typing import Literal

from pydantic import AliasChoices, Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class TtsServerSettings(BaseSettings):
    """TTS sunucusu çalışma zamanı ayarları."""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        populate_by_name=True,
        extra="ignore",
    )

    tts_auth_token: str = Field(
        default="dev-tts-secret-token-change-me-32chars",
        validation_alias=AliasChoices("TTS_API_TOKEN", "TTS_AUTH_TOKEN", "api_token", "tts_auth_token"),
    )
    tts_engine: Literal["voxcpm2", "xtts2", "mock"] = Field(
        default="voxcpm2",
        validation_alias=AliasChoices("TTS_ENGINE", "engine", "tts_engine"),
    )
    tts_allow_mock: bool = Field(
        default=False,
        validation_alias=AliasChoices("TTS_ALLOW_MOCK", "allow_mock", "tts_allow_mock"),
    )
    tts_model_id: str = Field(
        default="openbmb/VoxCPM2",
        validation_alias=AliasChoices("TTS_MODEL_ID", "model_id", "tts_model_id"),
    )
    tts_voices_dir: Path = Field(
        default=Path("/tmp/seslikitap_voices"),
        validation_alias=AliasChoices("TTS_VOICES_DIR", "voices_dir", "tts_voices_dir"),
    )
    hf_home: Path = Field(
        default=Path("/tmp/seslikitap_hf"),
        validation_alias=AliasChoices("HF_HOME", "hf_home"),
    )
    tts_device: str = Field(
        default="cuda",
        validation_alias=AliasChoices("TTS_DEVICE", "device", "tts_device"),
    )
    tts_max_batch_items: int = Field(
        default=32,
        ge=1,
        le=64,
        validation_alias=AliasChoices("TTS_MAX_BATCH_ITEMS", "max_batch_items", "tts_max_batch_items"),
    )
    tts_max_text_chars: int = Field(
        default=600,
        ge=50,
        le=2000,
        validation_alias=AliasChoices("TTS_MAX_TEXT_CHARS", "max_text_chars", "tts_max_text_chars"),
    )
    tts_max_cached_voices: int = Field(
        default=50,
        ge=1,
        le=500,
        validation_alias=AliasChoices("TTS_MAX_CACHED_VOICES", "max_cached_voices", "tts_max_cached_voices"),
    )
    log_level: str = Field(
        default="INFO",
        validation_alias=AliasChoices("LOG_LEVEL", "log_level"),
    )


ServerSettings = TtsServerSettings
