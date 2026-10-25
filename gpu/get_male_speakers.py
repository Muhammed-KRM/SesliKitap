import io
from pathlib import Path
import modal

app = modal.App("find-male-speaker")
volume = modal.Volume.from_name("seslikitap-tts-cache", create_if_missing=True)
image = modal.Image.debian_slim(python_version="3.11").apt_install("ffmpeg", "libsndfile1").pip_install("torch>=2.5.0", "torchaudio>=2.5.0", "soundfile>=0.12.1", "numpy<2.1.0", "datasets>=3.0.0")

@app.function(image=image, volumes={"/cache": volume}, timeout=300)
def find_male() -> list[dict]:
    import numpy as np, soundfile as sf, torch, torchaudio
    from datasets import load_dataset

    def resample(arr, orig_sr):
        t = torch.from_numpy(arr).float()
        if t.ndim == 1: t = t.unsqueeze(0)
        elif t.ndim == 2 and t.shape[0] > t.shape[1]: t = t.T
        if t.shape[0] > 1: t = t.mean(dim=0, keepdim=True)
        if orig_sr != 24000:
            t = torchaudio.transforms.Resample(orig_sr, 24000)(t)
        t = torch.clamp(t, -1.0, 1.0)
        buf = io.BytesIO()
        sf.write(buf, t.squeeze(0).numpy(), 24000, format="WAV", subtype="PCM_16")
        return buf.getvalue()

    import datasets
    fleurs = load_dataset("google/fleurs", "tr_tr", split="train", streaming=True)
    fleurs = fleurs.cast_column("audio", datasets.Audio(decode=False))
    candidates = []
    count = 0
    for item in fleurs:
        count += 1
        raw_bytes = item["audio"]["bytes"]
        arr, sr = sf.read(io.BytesIO(raw_bytes))
        dur = len(arr) / sr
        raw = (item.get("transcription") or item.get("raw_transcription") or "").strip()
        gender = item.get("gender")
        # Find male with 12 to 22 sec duration and rich text
        if gender == 0 and 10.0 <= dur <= 25.0 and len(raw) > 50:
            rms = float(np.sqrt(np.mean(arr**2)))
            if rms > 0.05:  # Good volume, no low level noise
                candidates.append({
                    "text": raw,
                    "dur": dur,
                    "rms": rms,
                    "wav": resample(arr, sr)
                })
                if len(candidates) >= 3:
                    break
        if count > 200:
            break
    return candidates

@app.local_entrypoint()
def main():
    root = Path(__file__).resolve().parents[1]
    res = find_male.remote()
    for i, c in enumerate(res):
        out_dir = root / "referans-ses" / "hazir_sesler" / f"male_candidate_{i+1}"
        out_dir.mkdir(parents=True, exist_ok=True)
        (out_dir / "reference.wav").write_bytes(c["wav"])
        (out_dir / "reference.txt").write_text(c["text"], encoding="utf-8")
        print(f"Candidate {i+1}: {c['dur']:.1f}s, RMS={c['rms']:.3f}, text={c['text'][:50]}...")
