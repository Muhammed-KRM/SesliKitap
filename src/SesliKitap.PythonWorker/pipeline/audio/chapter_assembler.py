"""Parça seslerini duraklamalarla akış halinde birleştiren ve örnek-kesinliğinde cümle zaman damgası üreten bileşen (Bölüm 4.15.6)."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import numpy as np
import soundfile as sf

from models.synthesis_models import (
    Chunk,
    ChunkAudio,
    Sentence,
    SentenceTimestamp,
    SynthesisInputSettingsModel,
)


@dataclass(slots=True)
class PauseSettings:
    """Bölüm birleştirme duraklama süreleri (milisaniye)."""

    cumle_arasi_ms: int = 420
    paragraf_arasi_ms: int = 950
    baslik_sonrasi_ms: int = 1400
    diyalog_ms: int = 320
    alt_parca_arasi_ms: int = 120

    @classmethod
    def from_input_settings(cls, s: SynthesisInputSettingsModel) -> PauseSettings:
        return cls(
            cumle_arasi_ms=s.cumle_arasi_ms,
            paragraf_arasi_ms=s.paragraf_arasi_ms,
            baslik_sonrasi_ms=s.baslik_sonrasi_ms,
            diyalog_ms=s.diyalog_ms,
        )


@dataclass(slots=True)
class AssemblyItem:
    """Birleştirilecek tek bir parça ve ses verisi çifti."""

    chunk: Chunk
    audio: ChunkAudio


def _trim_silence(samples: np.ndarray, sample_rate: int, threshold_dbfs: float = -45.0) -> np.ndarray:
    """Parça başındaki ve sonundaki -45 dBFS altı sessiz pencereleri (20 ms) kırpar."""
    if len(samples) == 0 or sample_rate <= 0:
        return samples

    frame_len = max(1, int(sample_rate * 0.02))
    if len(samples) < frame_len * 2:
        return samples

    linear_thresh = 10.0 ** (threshold_dbfs / 20.0)
    num_frames = len(samples) // frame_len
    frames = samples[: num_frames * frame_len].reshape(num_frames, frame_len)
    rms = np.sqrt(np.mean(np.square(frames, dtype=np.float64), axis=1))
    active = np.where(rms >= linear_thresh)[0]

    if len(active) == 0:
        return samples

    start_sample = int(active[0]) * frame_len
    end_frame = int(active[-1]) + 1
    end_sample = min(len(samples), end_frame * frame_len) if end_frame < num_frames else len(samples)
    return samples[start_sample:end_sample]


def _apply_micro_fade(samples: np.ndarray, sample_rate: int, fade_ms: float = 5.0) -> np.ndarray:
    """Tıklama sesini önlemek için parça başına ve sonuna 5 ms yumuşak geçiş (fade-in/out) uygular."""
    fade_len = int(round(sample_rate * (fade_ms / 1000.0)))
    if fade_len <= 1 or len(samples) < fade_len * 2:
        return samples

    out = samples.copy()
    ramp_in = np.linspace(0.0, 1.0, fade_len, dtype=np.float32)
    ramp_out = np.linspace(1.0, 0.0, fade_len, dtype=np.float32)
    out[:fade_len] *= ramp_in
    out[-fade_len:] *= ramp_out
    return out


def _resample_if_needed(samples: np.ndarray, orig_sr: int, target_sr: int) -> np.ndarray:
    """Örnekleme hızı hedef hızdan farklıysa doğrusal/vektörel yeniden örnekleme yapar."""
    if orig_sr == target_sr or len(samples) == 0 or orig_sr <= 0:
        return np.asarray(samples, dtype=np.float32)

    new_len = max(1, int(round(len(samples) * (target_sr / orig_sr))))
    x_old = np.linspace(0.0, 1.0, len(samples), endpoint=False)
    x_new = np.linspace(0.0, 1.0, new_len, endpoint=False)
    return np.interp(x_new, x_old, samples).astype(np.float32)


class ChapterAssembler:
    """Parça seslerini cümle/paragraf duraklamalarıyla diske akıtarak birleştirir ve kesin zaman damgası üretir."""

    def __init__(self, trim_silence: bool = True, fade_ms: float = 5.0) -> None:
        self._trim_silence = trim_silence
        self._fade_ms = fade_ms

    def assemble(
        self,
        items: list[AssemblyItem],
        sentences: list[Sentence],
        sample_rate: int,
        pauses: PauseSettings,
        out_path: Path,
    ) -> list[SentenceTimestamp]:
        """Parçaları WAV dosyasına yazar ve her cümle için milisaniye zaman damgası listesi döner."""
        out_path.parent.mkdir(parents=True, exist_ok=True)
        sentence_map: dict[int, Sentence] = {s.index: s for s in sentences}
        sentence_bounds: dict[int, list[int]] = {}  # sentence_index -> [start_sample, end_sample]

        current_sample = 0
        total_items = len(items)

        with sf.SoundFile(
            str(out_path),
            mode="w",
            samplerate=sample_rate,
            channels=1,
            subtype="PCM_16",
            format="WAV",
        ) as wav_file:
            for idx, item in enumerate(items):
                raw_samples = _resample_if_needed(
                    item.audio.samples, item.audio.sample_rate, sample_rate
                )
                if self._trim_silence:
                    raw_samples = _trim_silence(raw_samples, sample_rate)
                if self._fade_ms > 0:
                    raw_samples = _apply_micro_fade(raw_samples, sample_rate, self._fade_ms)

                chunk_len = len(raw_samples)
                chunk_start = current_sample
                wav_file.write(raw_samples)
                current_sample += chunk_len

                # Cümle zaman damgası ağırlık dağılımı
                weights = item.chunk.sentence_weights or []
                if len(weights) == 1:
                    s_idx = weights[0][0]
                    if s_idx not in sentence_bounds:
                        sentence_bounds[s_idx] = [chunk_start, current_sample]
                    else:
                        sentence_bounds[s_idx][1] = current_sample
                elif len(weights) > 1:
                    offset = chunk_start
                    total_w = sum(max(0.0001, w) for _, w in weights)
                    for w_pos, (s_idx, w) in enumerate(weights):
                        if w_pos == len(weights) - 1:
                            seg_end = current_sample
                        else:
                            seg_samples = int(round(chunk_len * (max(0.0001, w) / total_w)))
                            seg_end = min(current_sample, offset + seg_samples)
                        if s_idx not in sentence_bounds:
                            sentence_bounds[s_idx] = [offset, seg_end]
                        else:
                            sentence_bounds[s_idx][1] = seg_end
                        offset = seg_end

                # Son parçadan sonra duraklama eklenmez
                if idx + 1 < total_items:
                    pause_ms = self._resolve_pause_ms(item.chunk, pauses)
                    pause_samples = max(0, int(round(sample_rate * (pause_ms / 1000.0))))
                    if pause_samples > 0:
                        wav_file.write(np.zeros(pause_samples, dtype=np.float32))
                        current_sample += pause_samples

        timestamps: list[SentenceTimestamp] = []
        for s_idx in sorted(sentence_bounds.keys()):
            start_smp, end_smp = sentence_bounds[s_idx]
            start_ms = int(round(start_smp * 1000.0 / sample_rate))
            end_ms = int(round(end_smp * 1000.0 / sample_rate))
            sent_obj = sentence_map.get(s_idx)
            para_idx = sent_obj.paragraph if sent_obj is not None else 0
            display_text = sent_obj.display if sent_obj is not None else ""
            timestamps.append(
                SentenceTimestamp(
                    i=s_idx,
                    p=para_idx,
                    s=start_ms,
                    e=max(start_ms, end_ms),
                    t=display_text,
                )
            )

        return timestamps

    @staticmethod
    def _resolve_pause_ms(chunk: Chunk, pauses: PauseSettings) -> int:
        if chunk.is_subchunk:
            return pauses.alt_parca_arasi_ms
        if chunk.is_heading:
            return pauses.baslik_sonrasi_ms
        if chunk.is_paragraph_end:
            return pauses.paragraf_arasi_ms
        if chunk.is_dialogue_transition:
            return pauses.diyalog_ms
        return pauses.cumle_arasi_ms
