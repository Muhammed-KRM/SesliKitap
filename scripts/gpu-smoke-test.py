#!/usr/bin/env python3
"""Uzak GPU TTS sunucusunu uçtan uca doğrulayan duman testi (Smoke Test — Adım 3.5)."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import math
import sys
import time
from io import BytesIO
from pathlib import Path

import httpx
import numpy as np
import soundfile as sf

TURKISH_TEST_SENTENCES = [
    ("s-01", "Merhaba, bu SesliKitap duman testi için üretilmiş birinci Türkçe cümledir."),
    ("s-02", "Yirmi dokuz Ekim bin dokuz yüz yirmi üç tarihinde Cumhuriyet ilan edildi."),
    ("s-03", "Acaba bu akşam saat yirmi bir otuzda tren istasyonunda buluşabilir miyiz?"),
]


def build_reference_wav(duration_sec: float = 8.0, sample_rate: int = 24000) -> tuple[bytes, str]:
    """8 saniyelik temiz sentetik referans WAV ve SHA-256 özetini üretir."""
    n = round(duration_sec * sample_rate)
    t = np.linspace(0.0, duration_sec, n, endpoint=False, dtype=np.float32)
    wave = (
        0.40 * np.sin(2.0 * math.pi * 175.0 * t)
        + 0.18 * np.sin(2.0 * math.pi * 350.0 * t)
    ).astype(np.float32)
    with BytesIO() as bio:
        sf.write(bio, wave, sample_rate, format="WAV", subtype="PCM_16")
        raw = bio.getvalue()
    sha = hashlib.sha256(raw).hexdigest().lower()
    return raw, sha


def run_smoke_test(base_url: str, token: str, out_dir: Path) -> dict:
    url = base_url.rstrip("/")
    headers = {"Authorization": f"Bearer {token}"}
    out_dir.mkdir(parents=True, exist_ok=True)

    with httpx.Client(timeout=180.0) as client:
        t0 = time.perf_counter()
        health_resp = client.get(f"{url}/v1/health")
        cold_or_warm_ms = round((time.perf_counter() - t0) * 1000)
        health_resp.raise_for_status()
        health = health_resp.json()

        info_resp = client.get(f"{url}/v1/info", headers=headers)
        info_resp.raise_for_status()
        info = info_resp.json()

        raw_wav, voice_hash = build_reference_wav()
        put_resp = client.put(
            f"{url}/v1/voices/{voice_hash}",
            headers=headers,
            json={
                "audioB64": base64.b64encode(raw_wav).decode("ascii"),
                "promptText": "Merhaba, bu referans ses kaydı duman testi içindir.",
                "sampleRate": 24000,
            },
        )
        put_resp.raise_for_status()

        head_resp = client.head(f"{url}/v1/voices/{voice_hash}", headers=headers)
        assert head_resp.status_code == 200, f"HEAD 200 bekleniyordu, {head_resp.status_code} döndü"

        t_synth = time.perf_counter()
        synth_resp = client.post(
            f"{url}/v1/synthesize",
            headers=headers,
            json={
                "requestId": "smoke-test-001",
                "voiceHash": voice_hash,
                "language": "tr",
                "outputFormat": "flac",
                "params": {
                    "cfgValue": 2.0,
                    "inferenceTimesteps": 10,
                    "normalizeText": False,
                    "denoisePrompt": False,
                },
                "items": [
                    {"id": cid, "text": text, "seed": 42}
                    for cid, text in TURKISH_TEST_SENTENCES
                ],
            },
        )
        wall_ms = round((time.perf_counter() - t_synth) * 1000)
        synth_resp.raise_for_status()
        synth = synth_resp.json()

    total_audio_ms = 0
    saved_files: list[str] = []
    for item in synth["results"]:
        cid = item["id"]
        audio_bytes = base64.b64decode(item["audioB64"])
        samples, sr = sf.read(BytesIO(audio_bytes), dtype="float32")
        assert len(samples) > 0, f"{cid} boş ses döndürdü"
        assert item["rmsDbfs"] > -50.0, f"{cid} tamamen sessiz ({item['rmsDbfs']} dBFS)"
        out_wav = out_dir / f"{cid}.wav"
        sf.write(out_wav, samples, sr, format="WAV", subtype="PCM_16")
        saved_files.append(str(out_wav))
        total_audio_ms += int(item["durationMs"])

    rtf = round(synth["gpuMs"] / max(1, total_audio_ms), 4)
    summary = {
        "baseUrl": url,
        "health": health,
        "info": info,
        "healthLatencyMs": cold_or_warm_ms,
        "voiceHash": voice_hash,
        "wallMs": wall_ms,
        "gpuMs": synth["gpuMs"],
        "totalAudioMs": total_audio_ms,
        "rtf": rtf,
        "savedFiles": saved_files,
    }
    print(json.dumps(summary, indent=2, ensure_ascii=False))
    return summary


def main() -> int:
    parser = argparse.ArgumentParser(description="SesliKitap GPU TTS Duman Testi")
    parser.add_argument("--url", default="http://localhost:8009", help="TTS sunucu adresi")
    parser.add_argument(
        "--token",
        default="dev-tts-secret-token-change-me-32chars",
        help="Bearer kimlik doğrulama anahtarı",
    )
    parser.add_argument(
        "--out-dir",
        default="temp/smoke_test_out",
        help="Üretilen WAV dosyalarının kaydedileceği klasör",
    )
    args = parser.parse_args()
    run_smoke_test(args.url, args.token, Path(args.out_dir))
    return 0


if __name__ == "__main__":
    sys.exit(main())
