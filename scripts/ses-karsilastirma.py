"""A/B kulak testi: aynı referans ses + aynı cümleler + aynı tohumla dağıtılmış modelleri karşılaştırır.

Kullanım (repo kökünden, worker venv'i ile):
    src\\SesliKitap.PythonWorker\\.venv\\Scripts\\python.exe scripts\\ses-karsilastirma.py

Girdi : referans-ses\\ses.wav (veya .mp3/.m4a/.ogg/.flac) + referans-ses\\ses.txt (referansta söylenenlerin birebir yazısı)
Çıktı : referans-ses\\sonuc\\<model>_<NN>.wav dosyaları + ekranda tonal oran / RTF tablosu
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import io
import subprocess
import sys
import time
from pathlib import Path

import httpx
import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "src" / "SesliKitap.PythonWorker"))

from pipeline.tts.quality_checker import tonal_ratio

TEST_SENTENCES = [
    "Raif Efendi sessiz bir adamdı. Berlin yıllarını kimseye anlatmazdı.",
    "Oyun tasarımı, oyuncuların anlamlı seçimler yapabildiği sistemler kurma sanatıdır.",
    "Kitabın özgün adı, Rules of Play: Game Design Fundamentals olarak yayımlanmıştır.",
    "2004 yılında Massachusetts Teknoloji Enstitüsü tarafından 1.250 sayfalık bir kaynak olarak basıldı.",
    "Peki ya kurallar değişirse? Oyuncu yine de aynı keyfi alır mıydı, yoksa bırakıp gider miydi?",
    "Gece yarısı, şehrin tüm ışıkları söndüğünde, yalnızca eski saatin tik takları duyuluyordu.",
]


def read_env(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    if path.is_file():
        for line in path.read_text(encoding="utf-8").splitlines():
            if "=" in line and not line.lstrip().startswith("#"):
                key, _, val = line.partition("=")
                values[key.strip()] = val.strip()
    return values


def prepare_reference(src: Path) -> bytes:
    """Referansı 24 kHz mono PCM16 WAV baytlarına çevirir (ffmpeg varsa onunla, yoksa soundfile ile)."""
    try:
        raw = subprocess.run(
            ["ffmpeg", "-v", "error", "-i", str(src), "-ar", "24000", "-ac", "1", "-f", "wav", "-"],
            check=True,
            capture_output=True,
        ).stdout
        return raw
    except (FileNotFoundError, subprocess.CalledProcessError):
        data, sr = sf.read(str(src), dtype="float32", always_2d=False)
        if data.ndim == 2:
            data = data.mean(axis=1)
        if sr != 24000:
            n = round(len(data) * 24000 / sr)
            data = np.interp(np.linspace(0, 1, n, endpoint=False), np.linspace(0, 1, len(data), endpoint=False), data)
        buf = io.BytesIO()
        sf.write(buf, data.astype(np.float32), 24000, format="WAV", subtype="PCM_16")
        return buf.getvalue()


def run_model(name: str, url: str, token: str, wav: bytes, prompt_text: str, out_dir: Path) -> list[dict]:
    headers = {"Authorization": f"Bearer {token}"}
    voice_hash = hashlib.sha256(wav).hexdigest().lower()
    rows: list[dict] = []
    with httpx.Client(timeout=httpx.Timeout(600.0, connect=30.0), headers=headers) as client:
        t0 = time.perf_counter()
        info = client.get(f"{url}/v1/info", timeout=600.0)
        info.raise_for_status()
        print(f"  [{name}] sunucu hazır ({time.perf_counter() - t0:.0f} sn, soğuk başlatma dahil): {info.json().get('modelId')}")

        put = client.put(
            f"{url}/v1/voices/{voice_hash}",
            json={"audioB64": base64.b64encode(wav).decode("ascii"), "promptText": prompt_text, "sampleRate": 24000},
        )
        put.raise_for_status()

        body = {
            "requestId": f"ab-{name}-{int(time.time())}",
            "voiceHash": voice_hash,
            "language": "tr",
            "outputFormat": "flac",
            "output": {"codec": "flac", "sampleRate": 24000},
            "params": {"cfgValue": 2.0, "inferenceTimesteps": 10, "seed": 42, "normalizeText": False, "denoisePrompt": False},
            "chunks": [{"id": f"{i + 1:02d}", "text": t, "seed": 42} for i, t in enumerate(TEST_SENTENCES)],
        }
        t1 = time.perf_counter()
        resp = client.post(f"{url}/v1/synthesize", json=body)
        resp.raise_for_status()
        wall = time.perf_counter() - t1
        data = resp.json()
        gpu_ms = int(data.get("gpuMs", 0))

        total_audio_ms = 0
        for item in data["results"]:
            if not item.get("ok", True) or not item.get("audioB64"):
                rows.append({"id": item["id"], "ok": False})
                continue
            samples, sr = sf.read(io.BytesIO(base64.b64decode(item["audioB64"])), dtype="float32")
            if samples.ndim == 2:
                samples = samples.mean(axis=1)
            sf.write(str(out_dir / f"{name}_{item['id']}.wav"), samples, sr, subtype="PCM_16")
            dur = len(samples) / sr
            total_audio_ms += int(dur * 1000)
            rows.append({"id": item["id"], "ok": True, "sure": dur, "tonal": tonal_ratio(samples, sr)})
        rtf = (gpu_ms / total_audio_ms) if total_audio_ms else float("nan")
        print(f"  [{name}] 6 cümle: ses={total_audio_ms / 1000:.1f} sn, GPU={gpu_ms / 1000:.1f} sn, RTF={rtf:.2f}, duvar saati={wall:.1f} sn")
    return rows


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--dir", default=str(ROOT / "referans-ses"))
    args = parser.parse_args()

    base = Path(args.dir)
    ref = next((p for p in [base / f"ses{e}" for e in (".wav", ".mp3", ".m4a", ".ogg", ".flac")] if p.is_file()), None)
    txt = base / "ses.txt"
    if ref is None or not txt.is_file():
        print(f"HATA: '{base}' içinde ses.(wav|mp3|m4a|ogg|flac) ve ses.txt bulunamadı. referans-ses\\README.md dosyasına bakın.")
        return 2

    env = read_env(ROOT / ".env")
    token = env.get("TTS_API_TOKEN", "")
    targets = {k: v for k, v in {"temel": env.get("TTS_MODAL_URL_BASE"), "trendyol": env.get("TTS_MODAL_URL_TRENDYOL")}.items() if v}
    if not targets or not token:
        print("HATA: .env içinde TTS_MODAL_URL_BASE / TTS_MODAL_URL_TRENDYOL yok. Önce scripts\\kur-modal-tts.ps1 çalıştırın.")
        return 2

    out_dir = base / "sonuc"
    out_dir.mkdir(parents=True, exist_ok=True)
    wav = prepare_reference(ref)
    ref_dur = sf.info(io.BytesIO(wav)).duration
    print(f"Referans: {ref.name} ({ref_dur:.1f} sn). Önerilen: 10-20 sn, temiz, yankısız.")
    prompt_text = txt.read_text(encoding="utf-8").strip()

    print("\nModeller çalıştırılıyor (ilk istekte soğuk başlatma + model indirme 5-10 dk sürebilir)...")
    summary: dict[str, list[dict]] = {}
    for name, url in targets.items():
        try:
            summary[name] = run_model(name, url, token, wav, prompt_text, out_dir)
        except Exception as exc:  # noqa: BLE001 - kullanıcıya açık hata göster
            print(f"  [{name}] HATA: {exc}")

    print("\nTonal oran (konuşma ~0.2-0.5; 0.75 üstü = saf ton/sinüs):")
    for name, rows in summary.items():
        vals = [f"{r['tonal']:.2f}" if r.get("ok") and r.get("tonal") is not None else "-" for r in rows]
        print(f"  {name:9s}: " + "  ".join(vals))
    print(f"\nDinleyin: {out_dir}\\  (dosyalar: temel_01.wav ... trendyol_06.wav). Hangisi daha doğal geliyor?")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
