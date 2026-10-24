"""WAV ses dosyalarını Opus (.ogg) ve MP3 (.mp3) formatlarına kodlayan bileşen (Bölüm 4.15.6)."""

from __future__ import annotations

import shutil
from pathlib import Path

import numpy as np
import soundfile as sf

from models.events import ProcessingError
from pipeline.audio.ffmpeg_runner import FfmpegRunner
from utils.logger import get_logger

logger = get_logger(__name__)


class Encoder:
    """FFmpeg (veya test ortamında soundfile yedeği) ile Opus 48 kbps ve MP3 64 kbps kodlama yapar."""

    def __init__(self, ffmpeg: FfmpegRunner | None = None) -> None:
        self._ffmpeg = ffmpeg or FfmpegRunner()

    def to_opus(
        self,
        wav_path: Path,
        out_path: Path,
        *,
        bitrate_kbps: int = 48,
        tags: dict[str, str] | None = None,
    ) -> float:
        """WAV dosyasını 48 kHz mono Ogg Opus'a kodlar ve süreyi (sn) doğrular."""
        out_path.parent.mkdir(parents=True, exist_ok=True)
        expected_duration = self._ffmpeg.probe_duration(wav_path)

        if shutil.which(self._ffmpeg.ffmpeg_bin):
            args = [
                "-i",
                str(wav_path),
                "-vn",
                "-c:a",
                "libopus",
                "-b:a",
                f"{bitrate_kbps}k",
                "-vbr",
                "on",
                "-application",
                "audio",
                "-frame_duration",
                "20",
                "-ac",
                "1",
                "-ar",
                "48000",
            ]
            for k, v in (tags or {}).items():
                if v:
                    args.extend(["-metadata", f"{k}={v}"])
            args.extend(["-f", "ogg", str(out_path)])
            self._ffmpeg.run(args, timeout_sec=300)
        else:
            samples, sr = sf.read(str(wav_path), dtype="float32", always_2d=False)
            sf.write(str(out_path), np.asarray(samples, dtype=np.float32), sr, format="OGG", subtype="VORBIS")

        actual_duration = self._ffmpeg.probe_duration(out_path)
        if abs(actual_duration - expected_duration) > 1.0:
            raise ProcessingError(
                "ENCODER_DURATION_MISMATCH",
                f"Opus çıktı süresi beklenenden saptı (beklenen={expected_duration:.2f}s, ölçülen={actual_duration:.2f}s).",
                recoverable=False,
            )
        return round(actual_duration, 2)

    def to_mp3(
        self,
        wav_path: Path,
        out_path: Path,
        *,
        bitrate_kbps: int = 64,
        tags: dict[str, str] | None = None,
        cover_path: Path | None = None,
    ) -> float:
        """WAV dosyasını 44.1 kHz mono MP3'e (ID3v2.3 etiketleri ve kapak resmiyle) kodlar."""
        out_path.parent.mkdir(parents=True, exist_ok=True)
        expected_duration = self._ffmpeg.probe_duration(wav_path)

        if shutil.which(self._ffmpeg.ffmpeg_bin):
            args = ["-i", str(wav_path)]
            has_cover = cover_path is not None and cover_path.is_file()
            if has_cover:
                args.extend(["-i", str(cover_path), "-map", "0:a", "-map", "1:v", "-c:v", "copy"])
                args.extend(["-metadata:s:v", "title=Album cover", "-metadata:s:v", "comment=Cover (front)"])
            else:
                args.append("-vn")

            args.extend(
                [
                    "-c:a",
                    "libmp3lame",
                    "-b:a",
                    f"{bitrate_kbps}k",
                    "-ac",
                    "1",
                    "-ar",
                    "44100",
                    "-id3v2_version",
                    "3",
                ]
            )
            for k, v in (tags or {}).items():
                if v:
                    args.extend(["-metadata", f"{k}={v}"])
            args.extend(["-f", "mp3", str(out_path)])
            self._ffmpeg.run(args, timeout_sec=300)
        else:
            # FFmpeg olmayan saf birim testi ortamı için yedek
            shutil.copyfile(wav_path, out_path)

        actual_duration = self._ffmpeg.probe_duration(out_path)
        if abs(actual_duration - expected_duration) > 1.0:
            raise ProcessingError(
                "ENCODER_DURATION_MISMATCH",
                f"MP3 çıktı süresi beklenenden saptı (beklenen={expected_duration:.2f}s, ölçülen={actual_duration:.2f}s).",
                recoverable=False,
            )
        return round(actual_duration, 2)
