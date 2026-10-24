"""ChapterAssembler ve LoudnessNormalizer birim testleri (Bölüm 4.15.8)."""

from __future__ import annotations

from pathlib import Path

import numpy as np
import pytest
import soundfile as sf

from models.synthesis_models import Chunk, ChunkAudio, Sentence
from pipeline.audio.chapter_assembler import AssemblyItem, ChapterAssembler, PauseSettings
from pipeline.audio.ffmpeg_runner import FfmpegRunner
from pipeline.audio.loudness_normalizer import LoudnessNormalizer, _build_atempo_chain


def test_chapter_assembler_sample_accurate_timestamps_within_1ms(tmp_path: Path) -> None:
    sr = 24000
    assembler = ChapterAssembler(trim_silence=False, fade_ms=5.0)
    pauses = PauseSettings(
        cumle_arasi_ms=420,
        paragraf_arasi_ms=950,
        baslik_sonrasi_ms=1400,
        diyalog_ms=320,
        alt_parca_arasi_ms=120,
    )

    sentences = [
        Sentence(index=0, paragraph=-1, display="Birinci Bölüm", spoken="Birinci Bölüm.", is_heading=True),
        Sentence(index=1, paragraph=0, display="İlk cümle.", spoken="İlk cümle."),
        Sentence(index=2, paragraph=0, display="İkinci cümle.", spoken="İkinci cümle."),
    ]

    # 3 parça: 1000 ms (başlık), 1500 ms (1. cümle, paragraf içi), 1200 ms (2. cümle, paragraf sonu)
    def _make_sine(dur_ms: int) -> np.ndarray:
        n = int(round(sr * (dur_ms / 1000.0)))
        t = np.linspace(0.0, dur_ms / 1000.0, n, endpoint=False, dtype=np.float32)
        return (0.25 * np.sin(2.0 * np.pi * 220.0 * t)).astype(np.float32)

    items = [
        AssemblyItem(
            chunk=Chunk(
                id="c-1",
                text="Birinci Bölüm.",
                sentence_weights=[(0, 1.0)],
                paragraph=-1,
                is_paragraph_end=True,
                is_heading=True,
            ),
            audio=ChunkAudio(chunk_id="c-1", samples=_make_sine(1000), sample_rate=sr),
        ),
        AssemblyItem(
            chunk=Chunk(
                id="c-2",
                text="İlk cümle.",
                sentence_weights=[(1, 1.0)],
                paragraph=0,
                is_paragraph_end=False,
            ),
            audio=ChunkAudio(chunk_id="c-2", samples=_make_sine(1500), sample_rate=sr),
        ),
        AssemblyItem(
            chunk=Chunk(
                id="c-3",
                text="İkinci cümle.",
                sentence_weights=[(2, 1.0)],
                paragraph=0,
                is_paragraph_end=True,
            ),
            audio=ChunkAudio(chunk_id="c-3", samples=_make_sine(1200), sample_rate=sr),
        ),
    ]

    out_wav = tmp_path / "assembled.wav"
    timestamps = assembler.assemble(items, sentences, sr, pauses, out_wav)

    assert len(timestamps) == 3

    # Cümle 0 (Başlık): 0 .. 1000 ms, ardından baslik_sonrasi_ms = 1400 ms -> Cümle 1 başlangıcı = 2400 ms
    assert abs(timestamps[0].s - 0) <= 1
    assert abs(timestamps[0].e - 1000) <= 1

    # Cümle 1: 2400 .. 3900 ms, ardından cumle_arasi_ms = 420 ms -> Cümle 2 başlangıcı = 4320 ms
    assert abs(timestamps[1].s - 2400) <= 1
    assert abs(timestamps[1].e - 3900) <= 1

    # Cümle 2: 4320 .. 5520 ms
    assert abs(timestamps[2].s - 4320) <= 1
    assert abs(timestamps[2].e - 5520) <= 1

    info = sf.info(str(out_wav))
    expected_total_sec = (1000 + 1400 + 1500 + 420 + 1200) / 1000.0
    assert info.duration == pytest.approx(expected_total_sec, abs=0.002)


def test_loudness_normalizer_produces_valid_wav(tmp_path: Path) -> None:
    sr = 24000
    t = np.linspace(0.0, 2.0, sr * 2, endpoint=False, dtype=np.float32)
    quiet = (0.05 * np.sin(2.0 * np.pi * 220.0 * t)).astype(np.float32)

    in_wav = tmp_path / "quiet.wav"
    out_wav = tmp_path / "norm.wav"
    sf.write(str(in_wav), quiet, sr, subtype="PCM_16", format="WAV")

    normalizer = LoudnessNormalizer()
    normalizer.normalize(in_wav, out_wav, target_lufs=-18.0, speaking_rate=1.0)

    assert out_wav.is_file()
    out_samples, _ = sf.read(str(out_wav), dtype="float32", always_2d=False)
    assert len(out_samples) > 0
    # Normalize edilmiş sinyalin RMS'i sessiz girdiden belirgin şekilde yüksek olmalı
    in_rms = float(np.sqrt(np.mean(np.square(quiet))))
    out_rms = float(np.sqrt(np.mean(np.square(out_samples))))
    assert out_rms > in_rms * 1.5


def test_loudness_normalizer_two_pass_and_fallback_with_mock_ffmpeg(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    assert _build_atempo_chain(1.0) == ""
    assert _build_atempo_chain(1.25) == "atempo=1.2500"
    assert _build_atempo_chain(2.5) == "atempo=2.0,atempo=1.2500"

    sr = 24000
    t = np.linspace(0.0, 1.0, sr, endpoint=False, dtype=np.float32)
    wave = (0.1 * np.sin(2.0 * np.pi * 220.0 * t)).astype(np.float32)
    in_wav = tmp_path / "in.wav"
    out_wav = tmp_path / "out.wav"
    sf.write(str(in_wav), wave, sr, subtype="PCM_16", format="WAV")

    class FakeFfmpeg(FfmpegRunner):
        def __init__(self, fail_json: bool = False) -> None:
            super().__init__(ffmpeg_bin="ffmpeg")
            self.fail_json = fail_json
            self.calls: list[list[str]] = []

        def run_json(self, args: list[str], *, timeout_sec: int = 300) -> dict[str, str]:
            if self.fail_json:
                raise RuntimeError("pass1 failed")
            return {
                "input_i": "-23.5",
                "input_tp": "-3.1",
                "input_lra": "6.5",
                "input_thresh": "-33.5",
                "target_offset": "0.2",
            }

        def run(self, args: list[str], *, timeout_sec: int = 300) -> str:
            self.calls.append(args)
            target = Path(args[-1])
            sf.write(str(target), wave, 48000, subtype="PCM_16", format="WAV")
            return ""

    monkeypatch.setattr("shutil.which", lambda _bin: "/usr/bin/ffmpeg")

    # 2-pass başarı
    ff_ok = FakeFfmpeg(fail_json=False)
    norm_ok = LoudnessNormalizer(ffmpeg=ff_ok)
    norm_ok.normalize(in_wav, out_wav, speaking_rate=1.2)
    assert len(ff_ok.calls) == 1
    assert "linear=true" in " ".join(ff_ok.calls[0])

    # 1-pass fallback
    ff_fb = FakeFfmpeg(fail_json=True)
    norm_fb = LoudnessNormalizer(ffmpeg=ff_fb)
    norm_fb.normalize(in_wav, out_wav, speaking_rate=1.2)
    assert len(ff_fb.calls) == 1
    assert "linear=true" not in " ".join(ff_fb.calls[0])


def test_chapter_assembler_multi_sentence_and_sub_chunks(tmp_path: Path) -> None:
    sr = 24000
    assembler = ChapterAssembler(trim_silence=True, fade_ms=5.0)
    pauses = PauseSettings()

    sentences = [
        Sentence(index=0, paragraph=0, display="Kısa.", spoken="Kısa."),
        Sentence(index=1, paragraph=0, display="Biraz daha uzun.", spoken="Biraz daha uzun."),
        Sentence(index=2, paragraph=1, display="— Merhaba dedi.", spoken="Merhaba dedi.", is_dialogue=True),
    ]

    t = np.linspace(0.0, 1.0, sr, endpoint=False, dtype=np.float32)
    sine = (0.2 * np.sin(2.0 * np.pi * 220.0 * t)).astype(np.float32)

    items = [
        AssemblyItem(
            chunk=Chunk(
                id="c-1",
                text="Kısa. Biraz daha uzun.",
                sentence_weights=[(0, 0.3), (1, 0.7)],
                paragraph=0,
                is_paragraph_end=True,
                is_dialogue_transition=True,
            ),
            audio=ChunkAudio(chunk_id="c-1", samples=sine, sample_rate=sr),
        ),
        AssemblyItem(
            chunk=Chunk(
                id="c-2",
                text="Merhaba",
                sentence_weights=[(2, 0.5)],
                paragraph=1,
                is_subchunk=True,
            ),
            audio=ChunkAudio(chunk_id="c-2", samples=sine, sample_rate=sr),
        ),
        AssemblyItem(
            chunk=Chunk(
                id="c-3",
                text="dedi.",
                sentence_weights=[(2, 0.5)],
                paragraph=1,
                is_paragraph_end=True,
            ),
            audio=ChunkAudio(chunk_id="c-3", samples=sine, sample_rate=sr),
        ),
    ]

    out_wav = tmp_path / "multi.wav"
    ts = assembler.assemble(items, sentences, sr, pauses, out_wav)
    assert len(ts) == 3
    assert ts[0].s < ts[0].e <= ts[1].s < ts[1].e < ts[2].s < ts[2].e

