from __future__ import annotations

import json
from datetime import UTC, datetime
from typing import Any

import httpx

from config import ensure_directories, settings
from utils.logger import get_current_trace_id, get_logger

logger = get_logger(__name__)


class PipelineLogger:
    """3 katmanlı hata-güvenli (fail-safe) pipeline loglayıcı (OtoEdit kalıbı).

    Katman 1: .NET API POST /api/internal/logs/pipeline (2 sn zaman aşımı)
    Katman 2: Yerel logs/pipeline_fallback.jsonl dosyası
    Katman 3: Standart Python logger
    """

    def __init__(self, api_base_url: str | None = None, api_key: str | None = None) -> None:
        self._base_url = (api_base_url or settings.api_base_url).rstrip("/")
        self._api_key = api_key or settings.internal_api_key.get_secret_value()
        ensure_directories()
        self._fallback_file = settings.logs_dir / "pipeline_fallback.jsonl"

    def log_event(
        self,
        *,
        asama: str,
        durum: str = "Devam",
        yuzde: int | None = None,
        mesaj: str | None = None,
        book_id: str | None = None,
        job_id: str | None = None,
        chapter_id: str | None = None,
        girdi_metadata: dict[str, Any] | str | None = None,
        cikti_metadata: dict[str, Any] | str | None = None,
        hata_mesaji: str | None = None,
        hata_detayi: str | None = None,
        sure_ms: int | None = None,
        trace_id: str | None = None,
    ) -> None:
        """Pipeline olayını asla istisna fırlatmadan (fail-safe) kaydeder."""
        resolved_trace = trace_id or (get_current_trace_id() if get_current_trace_id() != "-" else None)

        payload = {
            "bookId": book_id,
            "jobId": job_id,
            "chapterId": chapter_id,
            "asama": asama,
            "durum": durum,
            "yuzde": yuzde,
            "mesaj": mesaj,
            "girdiMetadata": self._serialize_meta(girdi_metadata),
            "ciktiMetadata": self._serialize_meta(cikti_metadata),
            "hataMesaji": hata_mesaji,
            "hataDetayi": hata_detayi,
            "kaynak": "Worker",
            "traceId": resolved_trace,
            "sureMs": sure_ms,
        }

        # Katman 1: .NET Internal Logs API
        try:
            with httpx.Client(timeout=2.0) as client:
                resp = client.post(
                    f"{self._base_url}/api/internal/logs/pipeline",
                    json=payload,
                    headers={"X-API-Key": self._api_key},
                )
                if resp.status_code in (200, 201, 202, 204):
                    return
                logger.warning("Internal pipeline log HTTP %d döndü, fallback'e düşülüyor.", resp.status_code)
        except Exception as http_err:
            logger.debug("Internal pipeline log API erişilemedi (%s), fallback'e yazılıyor.", http_err)

        # Katman 2: Yerel JSONL dosyası
        try:
            fallback_entry = {**payload, "loggedAtUtc": datetime.now(UTC).isoformat()}
            with self._fallback_file.open("a", encoding="utf-8") as f:
                f.write(json.dumps(fallback_entry, ensure_ascii=False) + "\n")
            return
        except Exception as file_err:
            # Katman 3: Standart konsol/dosya logger
            logger.error("Pipeline fallback dosyasına da yazılamadı (%s): %s", file_err, payload)

    @staticmethod
    def _serialize_meta(meta: dict[str, Any] | str | None) -> str | None:
        if meta is None:
            return None
        if isinstance(meta, str):
            return meta
        return json.dumps(meta, ensure_ascii=False)
