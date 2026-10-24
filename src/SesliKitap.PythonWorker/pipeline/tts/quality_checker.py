"""Üretilen TTS parça seslerinin otomatik sinyal kalite kontrolü (Bölüm 4.15.6)."""

from __future__ import annotations

import math
from dataclasses import dataclass, field

import numpy as np

from models.synthesis_models import Chunk, ChunkAudio
from utils.constants import (
    QUALITY_CLIPPING_THRESHOLD,
    QUALITY_MAX_CLIPPING_RATIO,
    QUALITY_MAX_INTERNAL_SILENCE_SEC,
    QUALITY_MIN_SPEECH_FRAME_RATIO,
    QUALITY_MS_PER_CHAR_MAX,
    QUALITY_MS_PER_CHAR_MIN,
    QUALITY_SILENCE_RMS_DBFS,
    QUALITY_SPEECH_FRAME_DBFS,
)


@dataclass(slots=True)
class QualityReport:
    """Tek bir parça sesinin kalite değerlendirme sonucu."""

    ok: bool
    reasons: list[str] = field(default_factory=list)
    score: float = 1.0


def _rms_dbfs(samples: np.ndarray) -> float:
    if len(samples) == 0:
        return -120.0
    mean_sq = float(np.mean(np.square(samples, dtype=np.float64)))
    if mean_sq <= 1e-12:
        return -120.0
    return 10.0 * math.log10(mean_sq)


class QualityChecker:
    """Üretilen TTS parça sesini 4 sinyal kriterine göre denetler ve puanlar."""

    def __init__(
        self,
        ms_per_char_min: float = QUALITY_MS_PER_CHAR_MIN,
        ms_per_char_max: float = QUALITY_MS_PER_CHAR_MAX,
        silence_rms_dbfs: float = QUALITY_SILENCE_RMS_DBFS,
        speech_frame_dbfs: float = QUALITY_SPEECH_FRAME_DBFS,
        min_speech_frame_ratio: float = QUALITY_MIN_SPEECH_FRAME_RATIO,
        clipping_threshold: float = QUALITY_CLIPPING_THRESHOLD,
        max_clipping_ratio: float = QUALITY_MAX_CLIPPING_RATIO,
        max_internal_silence_sec: float = QUALITY_MAX_INTERNAL_SILENCE_SEC,
    ) -> None:
        self.ms_per_char_min = ms_per_char_min
        self.ms_per_char_max = ms_per_char_max
        self.silence_rms_dbfs = silence_rms_dbfs
        self.speech_frame_dbfs = speech_frame_dbfs
        self.min_speech_frame_ratio = min_speech_frame_ratio
        self.clipping_threshold = clipping_threshold
        self.max_clipping_ratio = max_clipping_ratio
        self.max_internal_silence_sec = max_internal_silence_sec

    def check(self, chunk: Chunk, audio: ChunkAudio) -> QualityReport:
        """Parça sesini denetler; ihlalleri ve 0..1 arası kalite puanını döner."""
        reasons: list[str] = []
        penalty = 0.0

        samples = audio.samples
        sr = audio.sample_rate
        if sr <= 0 or len(samples) == 0:
            return QualityReport(ok=False, reasons=["mostly_silent"], score=0.0)

        spoken_text = (chunk.text or "").strip()
        char_len = max(1, len(spoken_text))
        dur_ms = len(samples) * 1000.0 / sr

        # 1) Süre / karakter oranı (çok kısa kelimelerde alt/üst tolerans genişletilir)
        effective_min = self.ms_per_char_min if char_len >= 8 else 15.0
        effective_max = self.ms_per_char_max if char_len >= 8 else 320.0
        ratio = dur_ms / char_len
        if ratio < effective_min or ratio > effective_max:
            reasons.append("duration_ratio")
            penalty += 0.35

        # 2) Genel sessizlik ve konuşma çerçevesi oranı (20 ms pencereler)
        overall_dbfs = _rms_dbfs(samples)
        frame_len = max(1, int(sr * 0.02))
        num_frames = len(samples) // frame_len

        speech_frames = 0
        max_silent_run = 0
        current_silent_run = 0
        speech_linear_thresh = 10.0 ** (self.speech_frame_dbfs / 20.0)

        if num_frames > 0:
            trimmed = samples[: num_frames * frame_len].reshape(num_frames, frame_len)
            frame_rms = np.sqrt(np.mean(np.square(trimmed, dtype=np.float64), axis=1))
            for r_val in frame_rms:
                if r_val >= speech_linear_thresh:
                    speech_frames += 1
                    if current_silent_run > max_silent_run:
                        max_silent_run = current_silent_run
                    current_silent_run = 0
                else:
                    current_silent_run += 1
            if current_silent_run > max_silent_run:
                max_silent_run = current_silent_run

            speech_ratio = speech_frames / num_frames
        else:
            speech_ratio = 1.0 if overall_dbfs >= self.speech_frame_dbfs else 0.0

        if overall_dbfs < self.silence_rms_dbfs or speech_ratio < self.min_speech_frame_ratio:
            reasons.append("mostly_silent")
            penalty += 0.50

        # 3) Kırpılma (Clipping)
        clipped_count = int(np.sum(np.abs(samples) >= self.clipping_threshold))
        clipping_ratio = clipped_count / len(samples)
        if clipping_ratio > self.max_clipping_ratio:
            reasons.append("clipping")
            penalty += 0.30

        # 4) Uzun iç sessizlik (> 2.5 sn)
        max_silence_sec = max_silent_run * 0.02
        if max_silence_sec > self.max_internal_silence_sec:
            reasons.append("long_gap")
            penalty += 0.25

        score = round(max(0.0, 1.0 - penalty), 4)
        return QualityReport(ok=len(reasons) == 0, reasons=reasons, score=score)
