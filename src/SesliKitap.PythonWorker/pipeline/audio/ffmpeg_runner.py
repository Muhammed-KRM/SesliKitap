from __future__ import annotations

import json
import re
import subprocess
from pathlib import Path
from typing import Any

from models.events import ProcessingError
from utils.logger import get_logger

logger = get_logger(__name__)
_JSON_BLOCK_RE = re.compile(r"\{[\s\S]*\}")


class FfmpegRunner:
    """FFmpeg ve ffprobe çağrılarının tek merkezi sarmalayıcısı."""

    def __init__(self, ffmpeg_bin: str = "ffmpeg", ffprobe_bin: str = "ffprobe") -> None:
        self.ffmpeg_bin = ffmpeg_bin
        self.ffprobe_bin = ffprobe_bin

    def run(self, args: list[str], timeout_sec: int = 600) -> subprocess.CompletedProcess[str]:
        """FFmpeg komutunu çalıştırır; hata durumunda ProcessingError(FFMPEG_FAILED) fırlatır."""
        cmd = [self.ffmpeg_bin, "-y", "-hide_banner", "-loglevel", "error", *args]
        logger.debug("FFmpeg çalıştırılıyor: %s", " ".join(cmd))
        try:
            proc = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=timeout_sec,
                check=False,
            )
        except subprocess.TimeoutExpired as exc:
            raise ProcessingError("FFMPEG_TIMEOUT", f"FFmpeg {timeout_sec} sn içinde tamamlanamadı.", recoverable=True) from exc
        except FileNotFoundError as exc:
            raise ProcessingError("FFMPEG_NOT_FOUND", "FFmpeg yürütülebilir dosyası sistemde bulunamadı.") from exc

        if proc.returncode != 0:
            stderr_tail = (proc.stderr or "")[-2000:].strip()
            raise ProcessingError("FFMPEG_FAILED", f"FFmpeg hatası (kod {proc.returncode}): {stderr_tail}")

        return proc

    def probe_duration(self, path: Path, timeout_sec: int = 30) -> float:
        """ffprobe ile ses dosyasının saniye cinsinden süresini ölçer."""
        cmd = [
            self.ffprobe_bin,
            "-v",
            "error",
            "-show_entries",
            "format=duration",
            "-of",
            "default=noprint_wrappers=1:nokey=1",
            str(path),
        ]
        try:
            proc = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=timeout_sec,
                check=False,
            )
        except Exception as exc:
            raise ProcessingError("FFPROBE_FAILED", f"ffprobe çalıştırılamadı: {exc}") from exc

        if proc.returncode != 0 or not proc.stdout.strip():
            stderr_tail = (proc.stderr or "")[-1000:].strip()
            raise ProcessingError("FFPROBE_FAILED", f"ffprobe süre ölçemedi: {stderr_tail}")

        try:
            return float(proc.stdout.strip())
        except ValueError as exc:
            raise ProcessingError("FFPROBE_FAILED", f"Geçersiz ffprobe çıktısı: {proc.stdout}") from exc

    def run_json(self, args: list[str], timeout_sec: int = 600) -> dict[str, Any]:
        """stderr/stdout içinde JSON üreten FFmpeg filtrelerini (örn. loudnorm) çalıştırıp JSON'u ayrıştırır."""
        cmd = [self.ffmpeg_bin, "-y", "-hide_banner", *args]
        try:
            proc = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=timeout_sec,
                check=False,
            )
        except Exception as exc:
            raise ProcessingError("FFMPEG_FAILED", f"FFmpeg JSON komutu başarısız: {exc}") from exc

        if proc.returncode != 0:
            stderr_tail = (proc.stderr or "")[-2000:].strip()
            raise ProcessingError("FFMPEG_FAILED", f"FFmpeg hatası: {stderr_tail}")

        combined = f"{proc.stdout}\n{proc.stderr}"
        matches = _JSON_BLOCK_RE.findall(combined)
        if not matches:
            raise ProcessingError("FFMPEG_JSON_PARSE_ERROR", "FFmpeg çıktısında JSON bloğu bulunamadı.")

        try:
            return json.loads(matches[-1])
        except json.JSONDecodeError as exc:
            raise ProcessingError("FFMPEG_JSON_PARSE_ERROR", f"FFmpeg JSON çıktısı ayrıştırılamadı: {exc}") from exc
