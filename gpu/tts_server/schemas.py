"""Bölüm 4.8 HTTP TTS sunucu sözleşmesinin Pydantic v2 modelleri."""

from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, ConfigDict, Field, model_validator


def _to_camel(snake: str) -> str:
    parts = snake.split("_")
    return parts[0] + "".join(word.capitalize() for word in parts[1:])


class CamelModel(BaseModel):
    """Hem camelCase hem snake_case alan adlarını kabul eden ve camelCase serileştiren taban model."""

    model_config = ConfigDict(
        alias_generator=_to_camel,
        populate_by_name=True,
    )


class HealthResponse(CamelModel):
    status: str = "ok"
    service: str = "seslikitap-tts"
    version: str = "1.0.0"
    engine: str = "voxcpm2"
    model_loaded: bool = True
    gpu: str = "CPU"
    vram_free_mb: int = 0
    uptime_sec: int = 0


class InfoResponse(CamelModel):
    engine: str
    model_id: str
    model_revision: str
    lora_path: str | None = None
    sample_rate: int
    device: str = "cuda"
    gpu_name: str = "NVIDIA L4"
    max_batch: int = 32
    max_batch_items: int = 32
    max_chars_per_chunk: int = 600
    max_text_chars: int = 600
    supported_languages: list[str] = Field(default_factory=lambda: ["tr", "en"])
    supports_streaming: bool = False
    cached_voices: int = 0


class VoiceRegisterRequest(CamelModel):
    audio_b64: str = Field(..., min_length=16)
    prompt_text: str | None = None
    sample_rate: int = Field(default=24000, ge=8000, le=96000)


class VoiceRegisterResponse(CamelModel):
    voice_hash: str
    cached: bool = True
    duration_sec: float


class SynthesizeItem(CamelModel):
    id: str = Field(..., min_length=1, max_length=128)
    text: str = Field(..., min_length=1, max_length=5000)
    seed: int | None = None


class SynthesizeParams(CamelModel):
    cfg_value: float = Field(default=2.0, ge=0.5, le=10.0)
    inference_timesteps: int = Field(default=10, ge=1, le=64)
    seed: int | None = None
    normalize_text: bool = False
    denoise_prompt: bool = False


class OutputSpec(CamelModel):
    codec: Literal["flac", "wav_pcm16"] = "flac"
    sample_rate: int = Field(default=24000, ge=8000, le=96000)


class SynthesizeRequest(CamelModel):
    request_id: str = Field(..., min_length=1, max_length=128)
    engine: str | None = None
    voice_hash: str = Field(..., pattern=r"^[a-fA-F0-9]{64}$")
    language: str = Field(default="tr", min_length=2, max_length=10)
    output_format: Literal["flac", "wav_pcm16"] = "flac"
    output: OutputSpec | None = None
    sample_rate: int | None = None
    params: SynthesizeParams = Field(default_factory=SynthesizeParams)
    chunks: list[SynthesizeItem] | None = None
    items: list[SynthesizeItem] | None = None

    @model_validator(mode="after")
    def _unify_chunks_and_items(self) -> SynthesizeRequest:
        resolved = self.chunks if self.chunks is not None else self.items
        if not resolved:
            raise ValueError("En az 1 adet parça ('chunks' veya 'items') gönderilmelidir.")
        self.chunks = resolved
        self.items = resolved
        if self.output is not None:
            self.output_format = self.output.codec
            self.sample_rate = self.output.sample_rate
        return self


class SynthesizeResultItem(CamelModel):
    id: str
    ok: bool = True
    audio_b64: str | None = None
    duration_ms: int = 0
    attempts: int = 1
    sample_rate: int = 24000
    peak_dbfs: float = -120.0
    rms_dbfs: float = -120.0
    error: str | None = None


class SynthesizeResponse(CamelModel):
    request_id: str
    engine: str
    model_revision: str
    sample_rate: int = 24000
    gpu_ms: int
    results: list[SynthesizeResultItem]


class ErrorDetail(CamelModel):
    code: str
    message: str
    retryable: bool = False


class ErrorEnvelope(CamelModel):
    error: ErrorDetail
