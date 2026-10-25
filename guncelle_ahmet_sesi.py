import hashlib
import json
import subprocess
import sys
from pathlib import Path
import soundfile as sf
import httpx

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src" / "SesliKitap.PythonWorker"))

from config import settings
from services.minio_client import MinioStorageClient

def main():
    wav_path = ROOT / "referans-ses" / "hazir_sesler" / "01_tiyatrocu_tok_erkek" / "reference.wav"
    txt_path = ROOT / "referans-ses" / "hazir_sesler" / "01_tiyatrocu_tok_erkek" / "reference.txt"

    data, sr = sf.read(str(wav_path))
    dur = len(data) / sr
    text = txt_path.read_text(encoding="utf-8").strip()
    wav_bytes = wav_path.read_bytes()
    sha = hashlib.sha256(wav_bytes).hexdigest()
    profile_id = "01a17482-bd4e-789b-933b-5f8c7fd6f008"

    minio = MinioStorageClient()
    minio.upload_file(f"voices/{profile_id}/raw.wav", wav_path, content_type="audio/wav")
    minio.upload_file(f"voices/{profile_id}/reference.wav", wav_path, content_type="audio/wav")
    print(f"Uploaded new reference.wav to MinIO for {profile_id}")

    # Generate preview sample via Modal TTS server
    modal_url = "https://ustunmuhammed09--seslikitap-tts-voxcpm2-trendyol-voxcpms-edce4b.us-east.modal.run"
    sample_text = "Merhaba, ben Ahmet. Klasik ve edebi eserleri oturaklı, berrak bir tiyatrocu tonuyla seslendiriyorum."
    
    files = {
        "text": (None, sample_text),
        "prompt_text": (None, text),
        "prompt_wav": ("reference.wav", wav_bytes, "audio/wav"),
        "inference_timesteps": (None, "10"),
        "cfg_value": (None, "2.0")
    }
    
    token = settings.tts_api_token.get_secret_value() if settings.tts_api_token else ""
    headers = {"Authorization": f"Bearer {token}"} if token else {}

    print(f"Synthesizing preview sample via {modal_url}/v1/synthesize...")
    with httpx.Client(timeout=60.0) as client:
        resp = client.post(f"{modal_url}/v1/synthesize", files=files, headers=headers)
    if resp.status_code == 200:
        sample_wav_path = ROOT / "temp_sample.wav"
        sample_ogg_path = ROOT / "temp_sample.ogg"
        sample_wav_path.write_bytes(resp.content)
        
        # Convert to OGG Opus
        subprocess.run(["ffmpeg", "-y", "-i", str(sample_wav_path), "-c:a", "libopus", "-b:a", "48k", str(sample_ogg_path)], check=True)
        minio.upload_file(f"voices/{profile_id}/sample.ogg", sample_ogg_path, content_type="audio/ogg")
        print("Uploaded new sample.ogg to MinIO!")
        sample_wav_path.unlink(missing_ok=True)
        sample_ogg_path.unlink(missing_ok=True)
    else:
        print(f"Sample generation failed: {resp.status_code} - {resp.text}")

    # Update Postgres
    sql = f"""
    UPDATE voice_profiles
    SET referans_metin = '{text.replace("'", "''")}',
        referans_sure_sn = {dur:.2f},
        referans_hash = '{sha}',
        guncelleme_tarihi = NOW()
    WHERE id = '{profile_id}';
    """
    cmd = ["docker", "exec", "seslikitap-dev-postgres", "psql", "-U", "postgres", "-d", "seslikitap", "-c", sql]
    subprocess.run(cmd, check=True)
    print("PostgreSQL record updated successfully!")

if __name__ == "__main__":
    main()
