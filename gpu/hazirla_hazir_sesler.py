"""Hazır Türkçe Ses Profilleri Üretici / İndirici (Modal üzerinde çalışır).

SesliKitap projesine sıfırdan kullanıcı kaydı gerektirmeyen hazır profesyonel
Türkçe ses paketleri (presets) hazırlar ve referans-ses/hazir_sesler dizinine kaydeder.
"""

from __future__ import annotations

import io
from pathlib import Path
import modal

app = modal.App("seslikitap-presets-gen")
volume = modal.Volume.from_name("seslikitap-tts-cache", create_if_missing=True)

image = (
    modal.Image.debian_slim(python_version="3.11")
    .apt_install("ffmpeg", "libsndfile1")
    .pip_install(
        "torch>=2.5.0",
        "torchaudio>=2.5.0",
        "soundfile>=0.12.1",
        "numpy<2.1.0",
        "datasets>=3.0.0",
        "voxcpm==2.0.3",
        "huggingface-hub>=0.26.0",
    )
    .env({"HF_HOME": "/cache/hf"})
)


@app.function(gpu="L4", image=image, volumes={"/cache": volume}, timeout=600)
def generate_presets() -> dict[str, dict]:
    import numpy as np
    import soundfile as sf
    import torch
    import torchaudio
    from datasets import load_dataset
    from voxcpm import VoxCPM

    results: dict[str, dict] = {}

    def resample_to_24k_wav(arr: np.ndarray, orig_sr: int) -> bytes:
        t = torch.from_numpy(arr).float()
        if t.ndim == 1:
            t = t.unsqueeze(0)
        elif t.ndim == 2 and t.shape[0] > t.shape[1]:
            t = t.T
        if t.shape[0] > 1:
            t = t.mean(dim=0, keepdim=True)
        if orig_sr != 24000:
            resampler = torchaudio.transforms.Resample(orig_sr, 24000)
            t = resampler(t)
        t = torch.clamp(t, -1.0, 1.0)
        buf = io.BytesIO()
        sf.write(buf, t.squeeze(0).numpy(), 24000, format="WAV", subtype="PCM_16")
        return buf.getvalue()

    print("FLEURS Türkçe veri seti taranıyor...")
    fleurs = load_dataset("google/fleurs", "tr_tr", split="train", streaming=True)

    male_sample = None
    female_sample = None

    for item in fleurs:
        dur = len(item["audio"]["array"]) / item["audio"]["sampling_rate"]
        raw = (item.get("transcription") or item.get("raw_transcription") or item.get("raw_text") or "").strip()
        gender = item.get("gender")  # 0: male, 1: female (FLEURS mapping)
        if 8.0 <= dur <= 25.0 and len(raw) > 20:
            if gender == 0 and male_sample is None:
                male_sample = item
                male_sample["_text"] = raw
                print(f"  Erkek Anlatıcı bulundu ({dur:.1f} sn): {raw[:40]}...")
            elif gender == 1 and female_sample is None:
                female_sample = item
                female_sample["_text"] = raw
                print(f"  Kadın Anlatıcı bulundu ({dur:.1f} sn): {raw[:40]}...")
        if male_sample and female_sample:
            break

    # 1) Zeynep Hanım - Sakin / Masalsı Kadın Anlatıcı (FLEURS TR Stüdyo Kaydı)
    if female_sample:
        wav_bytes = resample_to_24k_wav(
            female_sample["audio"]["array"],
            female_sample["audio"]["sampling_rate"],
        )
        results["02_huzurlu_kadin"] = {
            "name": "Zeynep Hanım (Sakin & Masalsı)",
            "desc": "Romanlar, edebiyat ve kişisel gelişim kitapları için huzurlu, dinlendirici kadın anlatıcı.",
            "gender": "kadin",
            "text": female_sample["_text"],
            "wav": wav_bytes,
        }

    # 2) Murat - Dinamik Modern Anlatıcı (FLEURS TR Erkek Stüdyo Kaydı)
    if male_sample:
        wav_bytes = resample_to_24k_wav(
            male_sample["audio"]["array"],
            male_sample["audio"]["sampling_rate"],
        )
        results["03_dinamik_genc_erkek"] = {
            "name": "Murat (Dinamik & Modern)",
            "desc": "Bilim kurgu, polisiye ve akıcı macera romanları için modern, net erkek anlatıcı.",
            "gender": "erkek",
            "text": male_sample["_text"],
            "wav": wav_bytes,
        }

    # 3) Tok Sesli Tiyatrocu Erkek (VoxCPM Voice Design Modu)
    print("VoxCPM2 Voice Design ile Tok Tiyatrocu Erkek sesi üretiliyor...")
    vox = VoxCPM.from_pretrained("openbmb/VoxCPM2", load_denoiser=False, optimize=False, cache_dir="/cache/hf")
    theater_prompt_text = "Tiyatro sahnesi, yalnızca kelimelerin değil, insan ruhunun derinliklerinin yankılandığı yerdir."
    theater_design_input = f"(A deep, authoritative, warm male theater actor voice, theatrical storytelling pace, mature baritone) {theater_prompt_text}"

    theater_wav = vox.generate(
        text=theater_design_input,
        cfg_value=2.5,
        inference_timesteps=16,
    )
    theater_bytes = resample_to_24k_wav(theater_wav, getattr(vox.tts_model, "sample_rate", 48000))
    results["01_tiyatrocu_tok_erkek"] = {
        "name": "Ahmet Bey (Tok Tiyatrocu)",
        "desc": "Klasik romanlar, tarihi kurgu ve dramatik eserler için oturaklı, derin bariton tiyatrocu sahne tonu.",
        "gender": "erkek",
        "text": theater_prompt_text,
        "wav": theater_bytes,
    }

    # 4) Aylin - Stüdyo Spikeri (Trendyol TTS Yerleşik Ses)
    print("Trendyol TTS yerleşik stüdyo spikeri üretiliyor...")
    trendyol = VoxCPM.from_pretrained("Trendyol/Trendyol-TTS", load_denoiser=False, optimize=False, cache_dir="/cache/hf")
    spiker_text = "İstanbul Türkçesi ile seslendirilen kitaplar, dinleyiciye berrak ve akıcı bir edebiyat deneyimi sunar."
    spiker_wav = trendyol.generate(
        text=spiker_text,
        cfg_value=2.0,
        inference_timesteps=16,
    )
    spiker_bytes = resample_to_24k_wav(spiker_wav, getattr(trendyol.tts_model, "sample_rate", 48000))
    results["04_trendyol_spiker"] = {
        "name": "Aylin (Stüdyo Spikeri)",
        "desc": "Düz anlatı, araştırma-inceleme ve biyografiler için standart İstanbul Türkçesi stüdyo tonu.",
        "gender": "kadin",
        "text": spiker_text,
        "wav": spiker_bytes,
    }

    return results


@app.local_entrypoint()
def main():
    import json

    root = Path(__file__).resolve().parents[1]
    presets_dir = root / "referans-ses" / "hazir_sesler"
    presets_dir.mkdir(parents=True, exist_ok=True)

    print("Modal üzerinde hazır sesler üretiliyor ve indiriliyor...")
    data = generate_presets.remote()

    for folder_name, item in data.items():
        target_dir = presets_dir / folder_name
        target_dir.mkdir(parents=True, exist_ok=True)

        wav_path = target_dir / "reference.wav"
        txt_path = target_dir / "reference.txt"
        json_path = target_dir / "preset.json"

        wav_path.write_bytes(item["wav"])
        txt_path.write_text(item["text"], encoding="utf-8")

        meta = {
            "id": folder_name,
            "ad": item["name"],
            "aciklama": item["desc"],
            "cinsiyet": item["gender"],
            "referans_metin": item["text"],
        }
        json_path.write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"✓ Kaydedildi: {item['name']} -> {target_dir}")

    # Varsayılan olarak 01_tiyatrocu_tok_erkek sesini referans-ses/ses.wav ve ses.txt içine de kopyala
    default_src = presets_dir / "01_tiyatrocu_tok_erkek"
    if default_src.is_dir():
        ref_wav = root / "referans-ses" / "ses.wav"
        ref_txt = root / "referans-ses" / "ses.txt"
        ref_wav.write_bytes((default_src / "reference.wav").read_bytes())
        ref_txt.write_text((default_src / "reference.txt").read_text(encoding="utf-8"), encoding="utf-8")
        print("✓ Varsayılan ses olarak 'Ahmet Bey (Tok Tiyatrocu)' referans-ses/ses.wav olarak ayarlandı!")
