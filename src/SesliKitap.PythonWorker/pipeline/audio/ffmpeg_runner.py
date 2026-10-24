"""FFmpeg ve ffprobe çağrılarının tek merkezi sarmalayıcısı (Bölüm 4.15.6)."""

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
    """FFmpeg/ffprobe komutlarını çalıştırır, zaman aşımı ve hata yönetimini üstlenir."""

    def __init__(self, ffmpeg_bin: str = "ffmpeg", ffprobe_bin: str = "ffprobe") -> None:
        self.ffmpeg_bin = ffmpeg_bin
        self.ffprobe_bin = ffprobe_bin

    def run(
        self,
        args: list[str],
        *,
        timeout_sec: int = 600,
        log_level: str = "error",
    ) -> subprocess.CompletedProcess[str]:
        """FFmpeg komutunu verilen argümanlarla çalıştırır."""
        cmd = [
            self.ffmpeg_bin,
            "-y",
            "-hide_banner",
            "-nostdin",
            "-loglevel",
            log_level,
            *args,
        ]
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
            raise ProcessingError(
                "FFMPEG_TIMEOUT",
                f"FFmpeg işlemi {timeout_sec} sn içinde tamamlanamadı.",
                recoverable=True,
            ) from exc
        except FileNotFoundError as exc:
            raise ProcessingError(
                "FFMPEG_NOT_FOUND",
                "Sistemde 'ffmpeg' ikili dosyası bulunamadı.",
                recoverable=False,
            ) from exc

        if proc.returncode != 0:
            stderr_tail = (proc.stderr or "").strip()[-2000:]
            raise ProcessingError(
                "FFMPEG_FAILED",
                f"FFmpeg hata kodu ({proc.returncode}): {stderr_tail}",
                recoverable=False,
            )
        return proc

    def probe_duration(self, path: Path, timeout_sec: int = 30) -> float:
        """ffprobe (veya yedek olarak soundfile) ile ses dosyasının süresini saniye olarak ölçer."""
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
            if proc.returncode == 0 and proc.stdout.strip():
                return float(proc.stdout.strip())
        except (FileNotFoundError, ValueError, subprocess.TimeoutExpired):
            pass

        try:
            import soundfile as sf

            info = sf.info(str(path))
            return float(info.duration)
        except Exception as exc:
            raise ProcessingError(
                "FFPROBE_FAILED",
                f"Ses dosyası süresi okunamadı ({path.name}): {exc}",
                recoverable=False,
            ) from exc

    def run_json(self, args: list[str], timeout_sec: int = 300) -> dict[str, Any]:
        """loudnorm ölçümü gibi stderr'e JSON basan FFmpeg filtrelerinin çıktısını ayrıştırır."""
        proc = self.run(args, timeout_sec=timeout_sec, log_level="info")
        combined = f"{proc.stdout or ''}\n{proc.stderr or ''}"
        matches = list(_JSON_BLOCK_RE.finditer(combined))
        if not matches:
            raise ProcessingError(
                "FFMPEG_JSON_PARSE_ERROR",
                "FFmpeg çıktısında JSON bloğu bulunamadı.",
                recoverable=False,
            )
        raw_json = matches[-1].group(0)
        try:
            return json.loads(raw_json)
        except json.JSONDecodeError as exc:
            raise ProcessingError(
                "FFMPEG_JSON_PARSE_ERROR",
                f"FFmpeg JSON çıktısı ayrıştırılamadı: {exc}",
                recoverable=False,
            ) from exc
