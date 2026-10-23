from __future__ import annotations

import threading
from typing import Any

from utils.logger import get_logger

logger = get_logger(__name__)


class RabbitHeartbeatKeeper:
    """Uzun süren metin/ses işleme görevlerinde RabbitMQ BlockingConnection
    bağlantısının kopmasını önlemek için arka planda düzenli veri olaylarını işleyen yardımcı."""

    def __init__(self, connection: Any, interval_sec: float = 10.0) -> None:
        self._connection = connection
        self._interval = interval_sec
        self._stop_event = threading.Event()
        self._thread: threading.Thread | None = None

    def start(self) -> None:
        """Arka plan nabız iş parçacığını başlatır."""
        if not self._connection or not getattr(self._connection, "is_open", False):
            return

        self._stop_event.clear()
        self._thread = threading.Thread(
            target=self._run,
            daemon=True,
            name="RabbitMQ-HeartbeatKeeper",
        )
        self._thread.start()

    def stop(self) -> None:
        """Arka plan nabız iş parçacığını durdurur."""
        self._stop_event.set()
        if self._thread and self._thread.is_alive():
            self._thread.join(timeout=2.0)

    def _run(self) -> None:
        while not self._stop_event.is_set():
            try:
                if self._connection and getattr(self._connection, "is_open", False):
                    if hasattr(self._connection, "add_callback_threadsafe"):
                        self._connection.add_callback_threadsafe(
                            lambda: self._safe_process_events()
                        )
                    else:
                        self._safe_process_events()
            except Exception as exc:
                logger.debug("HeartbeatKeeper geçici durum: %s", exc)

            self._stop_event.wait(self._interval)

    def _safe_process_events(self) -> None:
        try:
            if self._connection and getattr(self._connection, "is_open", False):
                self._connection.process_data_events(time_limit=0)
        except Exception as exc:
            logger.debug("process_data_events geçici durum: %s", exc)
