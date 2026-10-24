"""Hazır bölüm ses dosyalarını bölüm işaretçili (chapters) tek bir M4B sesli kitap dosyasında birleştiren bileşen (Faz 8)."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from models.events import ProcessingError
from pipeline.audio.ffmpeg_runner import FfmpegRunner
from utils.logger import get_logger

logger = get_logger(__name__)


@dataclass(slots=True)
class M4bChapterInput:
    """M4B içine eklenecek sıralı bölüm girdisi."""

    sira_no: int
    baslik: str
    audio_path: Path
    duration_sec: float = 0.0


def _escape_ffmetadata(value: str) -> str:
    """FFmetadata özel karakterlerini (=, ;, #, \\, satır sonu) kaçırır."""
    escaped = value.replace("\\", "\\\\")
    escaped = escaped.replace("=", "\\=")
    escaped = escaped.replace(";", "\\;")
    escaped = escaped.replace("#", "\\#")
    escaped = escaped.replace("\r", " ").replace("\n", " ")
    return escaped.strip()


class M4bBuilder:
    """FFmpeg concat + ffmetadata ile AAC 64 kbps M4B sesli kitap üretir (Bölüm 4.15.6)."""

    def __init__(self, ffmpeg: FfmpegRunner | None = None) -> None:
        self._ffmpeg = ffmpeg or FfmpegRunner()

    def build(
        self,
        chapters: list[M4bChapterInput],
        output_path: Path,
        *,
        book_title: str,
        author: str | None = None,
        cover_path: Path | None = None,
        work_dir: Path | None = None,
        bitrate_kbps: int = 64,
    ) -> int:
        """Bölümleri birleştirerek bölüm işaretçili ve kapaklı .m4b dosyası oluşturur; dosya boyutunu (bayt) döner."""
        if not chapters:
            raise ProcessingError(
                "M4B_NO_CHAPTERS",
                "M4B oluşturmak için en az bir hazır bölüm gereklidir.",
                recoverable=False,
            )

        output_path.parent.mkdir(parents=True, exist_ok=True)
        temp_dir = work_dir or output_path.parent
        temp_dir.mkdir(parents=True, exist_ok=True)

        ordered = sorted(chapters, key=lambda c: c.sira_no)

        concat_list_path = temp_dir / "concat_inputs.txt"
        metadata_path = temp_dir / "ffmetadata.txt"

        concat_lines: list[str] = []
        meta_lines: list[str] = [
            ";FFMETADATA1",
            f"title={_escape_ffmetadata(book_title)}",
            f"album={_escape_ffmetadata(book_title)}",
            f"artist={_escape_ffmetadata(author or 'Bilinmeyen Yazar')}",
            "genre=Audiobook",
            "",
        ]

        cursor_ms = 0
        for ch in ordered:
            if not ch.audio_path.is_file():
                raise ProcessingError(
                    "M4B_CHAPTER_MISSING",
                    f"Bölüm ses dosyası bulunamadı: {ch.audio_path.name}",
                    recoverable=False,
                )

            safe_posix = ch.audio_path.resolve().as_posix().replace("'", "'\\''")
            concat_lines.append(f"file '{safe_posix}'")

            probed_sec = self._ffmpeg.probe_duration(ch.audio_path)
            dur_sec = probed_sec if probed_sec > 0 else max(0.1, ch.duration_sec)
            duration_ms = max(1, round(dur_sec * 1000))
            start_ms = cursor_ms
            end_ms = start_ms + duration_ms
            cursor_ms = end_ms

            chapter_title = ch.baslik.strip() or f"Bölüm {ch.sira_no}"
            meta_lines.extend(
                [
                    "[CHAPTER]",
                    "TIMEBASE=1/1000",
                    f"START={start_ms}",
                    f"END={end_ms}",
                    f"title={_escape_ffmetadata(chapter_title)}",
                    "",
                ]
            )

        concat_list_path.write_text("\n".join(concat_lines) + "\n", encoding="utf-8")
        metadata_path.write_text("\n".join(meta_lines), encoding="utf-8")

        args: list[str] = [
            "-f",
            "concat",
            "-safe",
            "0",
            "-i",
            str(concat_list_path),
            "-f",
            "ffmetadata",
            "-i",
            str(metadata_path),
        ]

        has_cover = cover_path is not None and cover_path.is_file()
        if has_cover and cover_path is not None:
            args.extend(["-i", str(cover_path)])

        args.extend(
            [
                "-map",
                "0:a",
                "-map_metadata",
                "1",
                "-map_chapters",
                "1",
                "-c:a",
                "aac",
                "-b:a",
                f"{bitrate_kbps}k",
                "-ac",
                "1",
                "-ar",
                "44100",
            ]
        )

        if has_cover:
            args.extend(
                [
                    "-map",
                    "2:v",
                    "-c:v",
                    "copy",
                    "-disposition:v:0",
                    "attached_pic",
                ]
            )
        else:
            args.append("-vn")

        args.extend(
            [
                "-movflags",
                "+faststart",
                "-f",
                "mp4",
                str(output_path),
            ]
        )

        self._ffmpeg.run(args, timeout_sec=1800)

        if not output_path.is_file() or output_path.stat().st_size == 0:
            raise ProcessingError(
                "M4B_EMPTY_OUTPUT",
                "M4B çıktısı oluşturulamadı veya boş.",
                recoverable=False,
            )

        size_bytes = output_path.stat().st_size
        logger.info(
            "M4B sesli kitap oluşturuldu: %s (%d bölüm, %d bayt)",
            output_path.name,
            len(ordered),
            size_bytes,
        )
        return size_bytes
