"""TTS parça önbelleği: FLAC olarak MinIO'da saklar, Redis'te indeksler (Bölüm 4.15.6)."""

from __future__ import annotations

import hashlib
import io
import json
from typing import Any

import numpy as np
import soundfile as sf

from models.synthesis_models import ChunkAudio
from models.voice_models import VoiceParams
from services.minio_client import MinioStorageClient
from services.redis_client import get_redis
from utils.constants import RedisKeys, StoragePaths
from utils.logger import get_logger

logger = get_logger(__name__)

_CACHE_TTL_SEC = 90 * 24 * 3600  # 90 gün


class ChunkCache:
    """Parça seslerini MinIO'da (FLAC) saklar, Redis'te indeksler."""

    def __init__(
        self,
        minio: MinioStorageClient,
        redis_client: Any | None = None,
    ) -> None:
        self._minio = minio
        self._redis = redis_client

    @staticmethod
    def key(
        engine_family: str,
        model_rev: str,
        voice_hash: str,
        params: VoiceParams,
        spoken_text: str,
    ) -> str:
        """Deterministik SHA-256 önbellek anahtarı üretir (konusmaHizi hariç, family bazlı)."""
        payload = json.dumps(
            {
                "f": engine_family,
                "m": model_rev,
                "v": voice_hash,
                "p": params.cache_dict(),
                "t": spoken_text.strip(),
            },
            ensure_ascii=False,
            sort_keys=True,
        )
        return hashlib.sha256(payload.encode("utf-8")).hexdigest()

    def _get_redis(self) -> Any | None:
        if self._redis is not None:
            return self._redis
        try:
            return get_redis()
        except Exception:
            return None

    def get(self, cache_key: str, chunk_id: str = "") -> ChunkAudio | None:
        """Tek bir önbellek anahtarını kontrol eder ve varsa ChunkAudio döner."""
        found = self.get_many([cache_key], {cache_key: chunk_id or cache_key})
        return found.get(cache_key)

    def get_many(
        self,
        keys: list[str],
        chunk_id_map: dict[str, str] | None = None,
    ) -> dict[str, ChunkAudio]:
        """Birden çok önbellek anahtarını Redis MGET (veya MinIO exists) ile kontrol edip indirir."""
        if not keys:
            return {}

        id_map = chunk_id_map or {}
        candidate_keys: list[str] = []
        r = self._get_redis()

        if r is not None:
            try:
                redis_keys = [RedisKeys.tts_chunk(k) for k in keys]
                values = r.mget(redis_keys) if hasattr(r, "mget") else [r.get(rk) for rk in redis_keys]
                for k, val in zip(keys, values, strict=False):
                    if val is not None:
                        candidate_keys.append(k)
            except Exception as exc:
                logger.warning("Redis önbellek sorgusu başarısız, MinIO stat'a düşülüyor: %s", exc)
                candidate_keys = list(keys)
        else:
            candidate_keys = list(keys)

        results: dict[str, ChunkAudio] = {}
        for k in candidate_keys:
            obj_key = StoragePaths.tts_chunk_object(k)
            try:
                if r is None and not self._minio.exists(obj_key):
                    continue
                raw_bytes = self._minio.download_bytes(obj_key)
                samples, sr = sf.read(io.BytesIO(raw_bytes), dtype="float32", always_2d=False)
                if samples.ndim > 1:
                    samples = np.mean(samples, axis=1).astype(np.float32)
                else:
                    samples = np.asarray(samples, dtype=np.float32)

                results[k] = ChunkAudio(
                    chunk_id=id_map.get(k, k),
                    samples=samples,
                    sample_rate=int(sr),
                    attempts=0,
                    engine_id="cache",
                    from_cache=True,
                )
            except Exception as exc:
                logger.debug("Önbellek nesnesi okunamadı (%s): %s", obj_key, exc)

        return results

    def put(self, cache_key: str, audio: ChunkAudio) -> None:
        """Üretilen parça sesini FLAC olarak MinIO'ya yazar ve Redis'te indeksler."""
        obj_key = StoragePaths.tts_chunk_object(cache_key)
        buf = io.BytesIO()
        sf.write(buf, audio.samples, audio.sample_rate, format="FLAC", subtype="PCM_16")
        flac_bytes = buf.getvalue()

        self._minio.upload_bytes(obj_key, flac_bytes, content_type="audio/flac")

        r = self._get_redis()
        if r is not None:
            try:
                rk = RedisKeys.tts_chunk(cache_key)
                if hasattr(r, "setex"):
                    r.setex(rk, _CACHE_TTL_SEC, "1")
                else:
                    r.set(rk, "1")
            except Exception as exc:
                logger.warning("Redis önbellek indeksleme hatası (%s): %s", cache_key, exc)
