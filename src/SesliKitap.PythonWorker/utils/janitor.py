from __future__ import annotations

import shutil
import time
from pathlib import Path

from config import settings
from utils.logger import get_logger

logger = get_logger(__name__)


def run_janitor(max_age_hours: float = 6.0) -> int:
    """Başlangıçta temp/ altındaki eski geçici iş klasörlerini temizler."""
    if not settings.temp_dir.exists():
        return 0

    cutoff = time.time() - (max_age_hours * 3600)
    removed = 0
    for item in settings.temp_dir.iterdir():
        try:
            if item.stat().st_mtime < cutoff:
                if item.is_dir():
                    shutil.rmtree(item, ignore_errors=True)
                else:
                    item.unlink(missing_ok=True)
                removed += 1
        except Exception as exc:
            logger.warning("Geçici öğe silinemedi (%s): %s", item, exc)

    if removed > 0:
        logger.info("Janitor: %d eski geçici öğe temizlendi.", removed)
    return removed


def create_job_temp_dir(prefix: str) -> Path:
    """İş için izole geçici çalışma dizini oluşturur."""
    settings.temp_dir.mkdir(parents=True, exist_ok=True)
    job_dir = settings.temp_dir / f"{prefix}_{int(time.time() * 1000)}"
    job_dir.mkdir(parents=True, exist_ok=True)
    return job_dir


def cleanup_job_dir(job_dir: Path | None) -> None:
    """TEMP_RETENTION=after_job ise iş sonunda geçici klasörü siler."""
    if job_dir is None or settings.temp_retention == "keep":
        return
    try:
        if job_dir.exists():
            shutil.rmtree(job_dir, ignore_errors=True)
    except Exception as exc:
        logger.warning("İş geçici dizini silinemedi (%s): %s", job_dir, exc)


cleanup_job = cleanup_job_dir
