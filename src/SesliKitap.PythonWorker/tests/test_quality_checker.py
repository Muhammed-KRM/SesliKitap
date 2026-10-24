"""QualityChecker birim testleri (Bölüm 4.15.8)."""

from __future__ import annotations

import numpy as np

from models.synthesis_models import Chunk, ChunkAudio
from pipeline.tts.quality_checker import QualityChecker


def test_quality_checker_accepts_normal_speech_signal() -> None:
    checker = QualityChecker()
    text = "Raif Efendi sessiz bir adamdı."
    chunk = Chunk(id="c-1", text=text, sentence_weights=[(0, 1.0)])

    sr = 24000
    dur_sec = len(text) * 0.065  # 65 ms / karakter
    t = np.linspace(0.0, dur_sec, int(sr * dur_sec), endpoint=False, dtype=np.float32)
    samples = 0.25 * np.sin(2.0 * np.pi * 220.0 * t).astype(np.float32)
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
