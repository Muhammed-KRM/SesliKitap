from __future__ import annotations

import logging
import time
from collections.abc import Iterator
from contextlib import contextmanager


class Stopwatch:
    """Milisaniye cinsinden geçen süreyi ölçen basit kronometre."""

    def __init__(self) -> None:
        self._start = time.perf_counter()

    def elapsed_ms(self) -> int:
        return int(round((time.perf_counter() - self._start) * 1000.0))


@contextmanager
def timed(logger: logging.Logger, label: str) -> Iterator[dict[str, float]]:
    """Kod bloğunun çalışma süresini milisaniye olarak ölçer ve loglar."""
    state = {"elapsed_ms": 0.0}
    start = time.perf_counter()
    try:
        yield state
    finally:
        elapsed_ms = (time.perf_counter() - start) * 1000.0
        state["elapsed_ms"] = round(elapsed_ms, 2)
        logger.info("%s tamamlandı (%.1f ms)", label, elapsed_ms)
