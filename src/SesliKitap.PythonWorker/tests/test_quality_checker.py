"""QualityChecker birim testleri (Bölüm 4.15.8)."""

from __future__ import annotations

import numpy as np

from models.synthesis_models import Chunk, ChunkAudio
from pipeline.tts.quality_checker import QualityChecker, tonal_ratio


def _speech_like(sr: int, dur_sec: float, seed: int = 7) -> np.ndarray:
    """Geniş bantlı, hece hızında zarflanmış konuşma benzeri test sinyali (saf ton DEĞİL)."""
    rng = np.random.default_rng(seed)
    n = int(sr * dur_sec)
    raw = rng.standard_normal(n + 8).astype(np.float32)
    shaped = np.convolve(raw, np.ones(9, dtype=np.float32) / 9.0, mode="valid")[:n]
    t = np.linspace(0.0, dur_sec, n, endpoint=False, dtype=np.float32)
    return (shaped * (0.65 + 0.35 * np.sin(2.0 * np.pi * 4.0 * t)) * 0.3).astype(np.float32)


def test_quality_checker_accepts_normal_speech_signal() -> None:
    checker = QualityChecker()
    text = "Raif Efendi sessiz bir adamdı."
    chunk = Chunk(id="c-1", text=text, sentence_weights=[(0, 1.0)])

    sr = 24000
    dur_sec = len(text) * 0.065  # 65 ms / karakter
    samples = _speech_like(sr, dur_sec)
    audio = ChunkAudio(chunk_id="c-1", samples=samples, sample_rate=sr)

    report = checker.check(chunk, audio)

    assert report.ok is True
    assert report.reasons == []
    assert report.score == 1.0


def test_quality_checker_rejects_silent_signal() -> None:
    checker = QualityChecker()
    text = "Bu cümle tamamen sessiz üretildi."
    chunk = Chunk(id="c-1", text=text, sentence_weights=[(0, 1.0)])

    sr = 24000
    samples = np.zeros(int(sr * 2.0), dtype=np.float32)
    audio = ChunkAudio(chunk_id="c-1", samples=samples, sample_rate=sr)

    report = checker.check(chunk, audio)

    assert report.ok is False
    assert "mostly_silent" in report.reasons


def test_quality_checker_rejects_clipped_signal() -> None:
    checker = QualityChecker()
    text = "Bu cümle aşırı kırpılmış ses içeriyor."
    chunk = Chunk(id="c-1", text=text, sentence_weights=[(0, 1.0)])

    sr = 24000
    dur_sec = len(text) * 0.065
    samples = np.ones(int(sr * dur_sec), dtype=np.float32)
    audio = ChunkAudio(chunk_id="c-1", samples=samples, sample_rate=sr)

    report = checker.check(chunk, audio)

    assert report.ok is False
    assert "clipping" in report.reasons


def test_quality_checker_rejects_excessive_duration_ratio() -> None:
    checker = QualityChecker()
    text = "Kısa bir cümle ama çok uzun sürdü."
    chunk = Chunk(id="c-1", text=text, sentence_weights=[(0, 1.0)])

    sr = 24000
    dur_sec = 12.0  # ~350 ms/karakter > 160 ms/karakter
    t = np.linspace(0.0, dur_sec, int(sr * dur_sec), endpoint=False, dtype=np.float32)
    samples = 0.2 * np.sin(2.0 * np.pi * 220.0 * t).astype(np.float32)
    audio = ChunkAudio(chunk_id="c-1", samples=samples, sample_rate=sr)

    report = checker.check(chunk, audio)

    assert report.ok is False
    assert "duration_ratio" in report.reasons

def test_quality_checker_rejects_pure_sine_as_tonal_signal() -> None:
    """Regresyon: 'vuvuvuv' (harmonik sinüs + 4 Hz zarf) konuşma olarak kabul edilmemelidir."""
    checker = QualityChecker()
    text = "Raif Efendi sessiz bir adamdı. Berlin yıllarını kimseye anlatmazdı."
    chunk = Chunk(id="c-1", text=text, sentence_weights=[(0, 1.0)])

    sr = 24000
    dur_sec = len(text) * 0.065
    t = np.linspace(0.0, dur_sec, int(sr * dur_sec), endpoint=False, dtype=np.float32)
    carrier = 0.55 * np.sin(2 * np.pi * 190.0 * t) + 0.25 * np.sin(2 * np.pi * 380.0 * t)
    samples = (carrier * (0.65 + 0.35 * np.sin(2 * np.pi * 4.0 * t)) * 0.38).astype(np.float32)

    report = checker.check(chunk, ChunkAudio(chunk_id="c-1", samples=samples, sample_rate=sr))

    assert report.ok is False
    assert "tonal_signal" in report.reasons


def test_tonal_ratio_separates_tone_from_speech_like_signal() -> None:
    sr = 24000
    t = np.linspace(0.0, 3.0, sr * 3, endpoint=False, dtype=np.float32)
    tone = (0.4 * np.sin(2 * np.pi * 200.0 * t)).astype(np.float32)

    tone_ratio = tonal_ratio(tone, sr)
    speech_ratio = tonal_ratio(_speech_like(sr, 3.0), sr)

    assert tone_ratio is not None and tone_ratio > 0.75
    assert speech_ratio is not None and speech_ratio < 0.5


def test_tonal_ratio_returns_none_for_too_short_or_silent_audio() -> None:
    sr = 24000
    assert tonal_ratio(np.zeros(sr * 2, dtype=np.float32), sr) is None
    assert tonal_ratio(np.ones(100, dtype=np.float32), sr) is None
