from __future__ import annotations

import logging
import sys
import threading
from logging.handlers import RotatingFileHandler

from config import ensure_directories, settings

_trace_local = threading.local()


def set_current_trace_id(trace_id: str | None) -> None:
    """Mevcut iş parçacığı için izleme kimliğini (trace_id) ayarlar."""
    _trace_local.trace_id = trace_id or "-"


def get_current_trace_id() -> str:
    """Mevcut iş parçacığının izleme kimliğini döner."""
    return getattr(_trace_local, "trace_id", "-")


class TraceIdFilter(logging.Filter):
    """Her log kaydına thread-local trace_id alanını ekleyen filtre."""

    def filter(self, record: logging.LogRecord) -> bool:
        if not hasattr(record, "trace_id"):
            record.trace_id = get_current_trace_id()
        return True


def setup_logging() -> None:
    """Konsol ve dönen dosya (RotatingFileHandler) loglamasını yapılandırır."""
    ensure_directories()
    root_logger = logging.getLogger()
    if root_logger.handlers:
        return

    level = getattr(logging, settings.log_level.upper(), logging.INFO)
    root_logger.setLevel(level)

    fmt = logging.Formatter(
        fmt="%(asctime)s | %(levelname)-7s | [%(trace_id)s] %(name)s | %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )
    trace_filter = TraceIdFilter()

    console_handler = logging.StreamHandler(sys.stdout)
    console_handler.setFormatter(fmt)
    console_handler.addFilter(trace_filter)
    root_logger.addHandler(console_handler)

    file_path = settings.logs_dir / "worker.log"
    file_handler = RotatingFileHandler(
        file_path,
        maxBytes=10 * 1024 * 1024,
        backupCount=5,
        encoding="utf-8",
    )
    file_handler.setFormatter(fmt)
    file_handler.addFilter(trace_filter)
    root_logger.addHandler(file_handler)

    for noisy in ("pika", "httpx", "httpcore", "urllib3", "fitz", "ebooklib"):
        logging.getLogger(noisy).setLevel(logging.WARNING)


def get_logger(name: str) -> logging.Logger:
    """Adlandırılmış logger döner."""
    return logging.getLogger(name)
