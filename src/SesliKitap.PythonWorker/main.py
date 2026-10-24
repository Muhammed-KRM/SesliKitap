from __future__ import annotations

import signal
import sys
import threading
import time
from collections.abc import Callable

from config import ensure_directories, settings
from consumers.base_consumer import BaseConsumer
from consumers.book_import_consumer import BookImportConsumer
from consumers.export_consumer import ExportConsumer
from consumers.synthesis_consumer import SynthesisConsumer
from consumers.voice_profile_consumer import VoiceProfileConsumer
from utils.janitor import run_janitor
from utils.logger import get_logger, setup_logging

logger = get_logger("seslikitap.worker.main")


class ConsumerSupervisor:
    """Consumer thread'lerini başlatır, izler ve beklenmedik çökmede 10 sn sonra yeniden başlatır."""

    def __init__(self, factories: dict[str, Callable[[], BaseConsumer]]) -> None:
        self._factories = factories
        self._instances: dict[str, BaseConsumer] = {}
        self._threads: dict[str, threading.Thread] = {}
        self._stop_event = threading.Event()

    def start_all(self) -> None:
        for name in self._factories:
            self._spawn(name)

    def _spawn(self, name: str) -> None:
        consumer = self._factories[name]()
        self._instances[name] = consumer
        thread = threading.Thread(
            target=consumer.start,
            name=f"consumer-{name}",
            daemon=True,
        )
        self._threads[name] = thread
        thread.start()
        logger.info("Consumer thread başlatıldı: %s", name)

    def monitor_loop(self) -> None:
        while not self._stop_event.is_set():
            for name, thread in list(self._threads.items()):
                if not thread.is_alive() and not self._stop_event.is_set():
                    logger.error(
                        "Consumer thread beklenmedik şekilde sonlandı (%s). 10 sn sonra yeniden başlatılıyor...",
                        name,
                    )
                    if self._stop_event.wait(timeout=10.0):
                        return
                    self._spawn(name)
            self._stop_event.wait(timeout=2.0)

    def shutdown(self, timeout_sec: float = 30.0) -> None:
        if self._stop_event.is_set():
            return
        logger.info("Nazik kapanış başlatıldı (zaman aşımı: %.0f sn)...", timeout_sec)
        self._stop_event.set()
        for consumer in self._instances.values():
            consumer.stop()

        deadline = time.monotonic() + timeout_sec
        for name, thread in self._threads.items():
            remaining = max(0.1, deadline - time.monotonic())
            thread.join(timeout=remaining)
            if thread.is_alive():
                logger.warning("Consumer thread süresi içinde kapanmadı: %s", name)
            else:
                logger.info("Consumer thread kapandı: %s", name)


def main() -> int:
    ensure_directories()
    setup_logging()

    logger.info("============================================================")
    logger.info("SesliKitap Python Worker Başlatılıyor...")
    logger.info(
        "RabbitMQ : %s:%d (vhost=%s, user=%s)",
        settings.rabbitmq_host,
        settings.rabbitmq_port,
        settings.rabbitmq_vhost,
        settings.rabbitmq_user,
    )
    logger.info(
        "MinIO    : %s (bucket=%s, secure=%s)",
        settings.minio_endpoint,
        settings.minio_bucket,
        settings.minio_secure,
    )
    logger.info("TTS Sıra : %s", ", ".join(settings.tts_engine_order))
    logger.info("Temp Dir : %s (tutma=%s)", settings.temp_dir, settings.temp_retention)
    logger.info("============================================================")

    removed_dirs = run_janitor()
    if removed_dirs > 0:
        logger.info("Başlangıç temizliği: %d eski geçici klasör silindi.", removed_dirs)

    supervisor = ConsumerSupervisor(
        factories={
            "BookImportConsumer": BookImportConsumer,
            "VoiceProfileConsumer": VoiceProfileConsumer,
            "SynthesisConsumer": SynthesisConsumer,
            "ExportConsumer": ExportConsumer,
        }
    )

    def _signal_handler(signum: int, _frame: object) -> None:
        sig_name = signal.Signals(signum).name
        logger.info("Sinyal alındı (%s), worker durduruluyor...", sig_name)
        supervisor.shutdown(timeout_sec=30.0)

    signal.signal(signal.SIGINT, _signal_handler)
    signal.signal(signal.SIGTERM, _signal_handler)

    supervisor.start_all()
    supervisor.monitor_loop()
    logger.info("SesliKitap Python Worker durduruldu.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
