"""EBU R128 iki geçişli ses yüksekliği normalleştirici (-18 LUFS, -1.5 dBTP) ve hız ayarlayıcı (Bölüm 4.15.6)."""

from __future__ import annotations

import shutil
from pathlib import Path

import numpy as np
import soundfile as sf

from pipeline.audio.ffmpeg_runner import FfmpegRunner
from utils.logger import get_logger

logger = get_logger(__name__)


def _build_atempo_chain(speed: float) -> str:
    """FFmpeg atempo filtresi (0.5..2.0 aralığı) için zincir oluşturur."""
    clamped = max(0.5, min(3.0, float(speed)))
    if abs(clamped - 1.0) < 1e-3:
        return ""
    if clamped <= 2.0:
        return f"atempo={clamped:.4f}"
    # 2.0 üstü için iki kademe
    half = clamped / 2.0
    return f"atempo=2.0,atempo={half:.4f}"


class LoudnessNormalizer:
    """Bölüm WAV dosyasını EBU R128 (-18 LUFS, -1.5 dBTP) standardına göre iki geçişli normalize eder."""

    def __init__(self, ffmpeg: FfmpegRunner | None = None) -> None:
        self._ffmpeg = ffmpeg or FfmpegRunner()

    def normalize(
        self,
        in_wav: Path,
        out_wav: Path,
        *,
        target_lufs: float = -18.0,
        true_peak_dbtp: float = -1.5,
        lra: float = 11.0,
        speaking_rate: float = 1.0,
        target_sample_rate: int = 48000,
    ) -> Path:
        """in_wav dosyasını iki geçişli loudnorm (+ varsa atempo) ile normalize edip out_wav'a yazar."""
        out_wav.parent.mkdir(parents=True, exist_ok=True)
        atempo_filter = _build_atempo_chain(speaking_rate)

        if shutil.which(self._ffmpeg.ffmpeg_bin):
            try:
                # 1. Geçiş: EBU R128 ölçümü
                pass1_filters = []
                if atempo_filter:
                    pass1_filters.append(atempo_filter)
                pass1_filters.append(
                    f"loudnorm=I={target_lufs:.1f}:TP={true_peak_dbtp:.1f}:LRA={lra:.1f}:print_format=json"
                )
                measured = self._ffmpeg.run_json(
                    [
                        "-i",
                        str(in_wav),
                        "-af",
                        ",".join(pass1_filters),
                        "-f",
                        "null",
                        "-",
                    ],
                    timeout_sec=300,
                )

                in_i = measured.get("input_i", "-24.0")
                in_tp = measured.get("input_tp", "-2.0")
                in_lra = measured.get("input_lra", "7.0")
                in_thresh = measured.get("input_thresh", "-34.0")
                offset = measured.get("target_offset", "0.0")

                # 2. Geçiş: Doğrusal/dinamik uygulama
                pass2_filters = []
                if atempo_filter:
                    pass2_filters.append(atempo_filter)
                pass2_filters.append(
                    f"loudnorm=I={target_lufs:.1f}:TP={true_peak_dbtp:.1f}:LRA={lra:.1f}:"
                    f"measured_I={in_i}:measured_TP={in_tp}:measured_LRA={in_lra}:"
                    f"measured_thresh={in_thresh}:offset={offset}:linear=true"
                )
                self._ffmpeg.run(
                    [
                        "-i",
                        str(in_wav),
                        "-af",
                        ",".join(pass2_filters),
                        "-ac",
                        "1",
                        "-ar",
                        str(target_sample_rate),
                        "-c:a",
                        "pcm_s16le",
                        str(out_wav),
                    ],
                    timeout_sec=300,
                )
                return out_wav
            except Exception as exc:
                logger.warning("İki geçişli loudnorm başarısız oldu, tek geçişe düşülüyor: %s", exc)
                fallback_filters = []
                if atempo_filter:
                    fallback_filters.append(atempo_filter)
                fallback_filters.append(
                    f"loudnorm=I={target_lufs:.1f}:TP={true_peak_dbtp:.1f}:LRA={lra:.1f}"
                )
                self._ffmpeg.run(
                    [
                        "-i",
                        str(in_wav),
                        "-af",
                        ",".join(fallback_filters),
                        "-ac",
                        "1",
                        "-ar",
                        str(target_sample_rate),
                        "-c:a",
                        "pcm_s16le",
                        str(out_wav),
                    ],
                    timeout_sec=300,
                )
                return out_wav

        # FFmpeg olmayan saf Python test ortamı için RMS tabanlı yedek normalleştirme
        samples, sr = sf.read(str(in_wav), dtype="float32", always_2d=False)
        samples = np.asarray(samples, dtype=np.float32)
        rms = float(np.sqrt(np.mean(np.square(samples, dtype=np.float64)))) if len(samples) > 0 else 0.0
        if rms > 1e-6:
            target_rms = 10.0 ** (target_lufs / 20.0)
            gain = target_rms / rms
            peak_limit = 10.0 ** (true_peak_dbtp / 20.0)
            scaled = np.clip(samples * gain, -peak_limit, peak_limit).astype(np.float32)
        else:
            scaled = samples
        sf.write(str(out_wav), scaled, sr, subtype="PCM_16", format="WAV")
        return out_wav
