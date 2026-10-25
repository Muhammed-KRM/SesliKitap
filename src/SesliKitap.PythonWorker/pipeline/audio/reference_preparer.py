"""Ham ses kaydını 24 kHz mono PCM16 WAV referans sesine dönüştüren ve doğrulayan hazırlayıcı."""

from __future__ import annotations

import shutil
from pathlib import Path

import numpy as np
import soundfile as sf

from models.events import ProcessingError
from models.voice_models import PreparedReference
from pipeline.audio.ffmpeg_runner import FfmpegRunner
from utils.hashing import sha256_file
from utils.logger import get_logger

logger = get_logger(__name__)

MIN_VOICE_SEC = 5.0
MAX_VOICE_SEC = 30.0
TARGET_SAMPLE_RATE = 24000


class ReferencePreparer:
    """Referans ses kaydını temizler, 5–30 sn aralığına getirir, -20 LUFS normalize eder ve SHA-256 hesaplar."""

    def __init__(self, ffmpeg: FfmpegRunner | None = None) -> None:
        self._ffmpeg = ffmpeg or FfmpegRunner()

    def prepare(self, raw_path: Path, out_wav_path: Path | None = None) -> PreparedReference:
        """Ham ses dosyasını işleyerek PreparedReference döndürür."""
        if not raw_path.is_file() or raw_path.stat().st_size == 0:
            raise ProcessingError(
                "VOICE_EMPTY_FILE",
                "Yüklenen ses dosyası boş veya okunamıyor.",
                recoverable=False,
            )

        target_path = out_wav_path or raw_path.with_name("reference.wav")
        tmp_clean = raw_path.with_name("_tmp_clean_24k.wav")

        # 1) FFmpeg mevcutsa: 24 kHz mono, baştaki/sondaki sessizlik temizliği, 70 Hz highpass ve -20 LUFS loudnorm
        ffmpeg_ok = False
        if shutil.which(self._ffmpeg.ffmpeg_bin):
            try:
                af_chain = (
                    "highpass=f=70,"
                    "loudnorm=I=-20:TP=-1.5:LRA=11"
                )
                self._ffmpeg.run(
                    [
                        "-i",
                        str(raw_path),
                        "-vn",
                        "-ac",
                        "1",
                        "-ar",
                        str(TARGET_SAMPLE_RATE),
                        "-af",
                        af_chain,
                        "-c:a",
                        "pcm_s16le",
                        str(tmp_clean),
                    ],
                    timeout_sec=120,
                )
                ffmpeg_ok = tmp_clean.is_file() and tmp_clean.stat().st_size > 44
            except ProcessingError as exc:
                logger.warning("FFmpeg filtre zinciri başarısız oldu, soundfile yedeği deneniyor: %s", exc)

        # 2) Yedek veya son okuma: soundfile ile yükle, mono/24k/süre/kırpma/normalizasyon uygula
        source_for_read = tmp_clean if ffmpeg_ok else raw_path
        try:
            samples, sr = sf.read(str(source_for_read), dtype="float32", always_2d=False)
        except Exception as exc:
            tmp_clean.unlink(missing_ok=True)
            raise ProcessingError(
                "VOICE_INVALID_FORMAT",
                "Ses dosyası bozuk veya desteklenmeyen bir kodek içeriyor. Lütfen WAV, MP3 veya M4A yükleyin.",
                recoverable=False,
            ) from exc
        finally:
            tmp_clean.unlink(missing_ok=True)

        arr = np.asarray(samples, dtype=np.float32)
        if arr.ndim == 2:
            arr = arr.mean(axis=1)

        if sr != TARGET_SAMPLE_RATE and arr.size > 0:
            new_len = max(1, round(arr.size * TARGET_SAMPLE_RATE / float(sr)))
            old_idx = np.linspace(0.0, 1.0, arr.size, endpoint=False)
            new_idx = np.linspace(0.0, 1.0, new_len, endpoint=False)
            arr = np.interp(new_idx, old_idx, arr).astype(np.float32)
            sr = TARGET_SAMPLE_RATE

        # Baş/son sessizlik kırpma (-45 dBFS ≈ 0.0056 genlik)
        if arr.size > 0:
            threshold = 10.0 ** (-45.0 / 20.0)
            active = np.flatnonzero(np.abs(arr) > threshold)
            if active.size > 0:
                pad = int(0.05 * sr)
                start_i = max(0, int(active[0]) - pad)
                end_i = min(arr.size, int(active[-1]) + pad)
                arr = arr[start_i:end_i]

        duration_sec = float(arr.size) / float(sr) if sr > 0 else 0.0
        if duration_sec < MIN_VOICE_SEC:
            raise ProcessingError(
                "VOICE_TOO_SHORT",
                f"Ses kaydı çok kısa ({duration_sec:.1f} sn). En az 5 saniyelik temiz bir kayıt yükleyin.",
                recoverable=False,
            )

        # > 30 sn ise ilk 30 sn içindeki son düşük enerjili noktadan kes
        if duration_sec > MAX_VOICE_SEC:
            max_samples = int(MAX_VOICE_SEC * sr)
            cut_idx = self._find_best_cut_point(arr[:max_samples], sr)
            arr = arr[:cut_idx]
            duration_sec = float(arr.size) / float(sr)

        # FFmpeg çalışmadıysa RMS tabanlı -20 dBFS normalizasyon uygula
        if not ffmpeg_ok and arr.size > 0:
            rms = float(np.sqrt(np.mean(np.square(arr, dtype=np.float64))))
            if rms > 1e-5:
                target_rms = 10.0 ** (-20.0 / 20.0)
                arr = np.clip(arr * (target_rms / rms), -0.95, 0.95).astype(np.float32)

        target_path.parent.mkdir(parents=True, exist_ok=True)
        sf.write(str(target_path), arr, sr, format="WAV", subtype="PCM_16")
        digest = sha256_file(target_path)

        return PreparedReference(
            wav_path=target_path,
            duration_sec=round(duration_sec, 2),
            sha256=digest,
        )

    @staticmethod
    def _find_best_cut_point(arr: np.ndarray, sr: int) -> int:
        """25–30 sn aralığındaki en sessiz 100 ms penceresini bulur."""
        min_idx = int(25.0 * sr)
        if arr.size <= min_idx:
            return arr.size
        win = max(1, int(0.1 * sr))
        tail = arr[min_idx:]
        if tail.size <= win:
            return arr.size
        n_windows = tail.size // win
        energies = [
            float(np.mean(np.square(tail[i * win : (i + 1) * win])))
            for i in range(n_windows)
        ]
        best_win = int(np.argmin(energies))
        return min_idx + (best_win * win)
