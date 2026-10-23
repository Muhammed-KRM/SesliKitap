from __future__ import annotations

import time

import redis

from config import settings
from utils.constants import RedisKeys
from utils.logger import get_logger

logger = get_logger(__name__)

_redis_instance: redis.Redis | None = None


def get_redis() -> redis.Redis:
    """Paylaşılan Redis istemcisini döner."""
    global _redis_instance
    if _redis_instance is None:
        _redis_instance = redis.Redis.from_url(
            settings.redis_url,
            socket_timeout=2.0,
            socket_connect_timeout=2.0,
            decode_responses=True,
        )
    return _redis_instance


class CancelChecker:
    """Sentez işinin iptal edilip edilmediğini Redis üzerinden (3 sn bellek önbellekli, fail-open) kontrol eder."""

    def __init__(self, redis_client: redis.Redis | None = None, cache_ttl_sec: float = 3.0) -> None:
        self._redis = redis_client
        self._cache_ttl = cache_ttl_sec
        self._cache: dict[str, tuple[float, bool]] = {}

    def is_cancelled(self, job_id: str) -> bool:
        now = time.monotonic()
        cached = self._cache.get(job_id)
        if cached is not None and (now - cached[0]) < self._cache_ttl:
            return cached[1]

        try:
            client = self._redis or get_redis()
            val = client.get(RedisKeys.job_cancel(job_id))
            cancelled = str(val) == "1"
            self._cache[job_id] = (now, cancelled)
            return cancelled
        except Exception as exc:
            logger.warning("Redis iptal kontrolü başarısız (fail-open, job_id=%s): %s", job_id, exc)
            return False
