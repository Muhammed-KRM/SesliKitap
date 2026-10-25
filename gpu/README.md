# SesliKitap — Uzak GPU TTS Sunucusu (`gpu/`)

Bu dizin, SesliKitap Python Worker'ının HTTP üzerinden çağırdığı durumsuz (stateless) GPU TTS sunucusunu barındırır. Aynı `tts_server` çekirdek paketi üç farklı ortamda değişiklik yapılmadan çalışır:

1. **Modal.com Serverless GPU (Birincil — L4 24 GB VRAM):** `modal_deploy.py` (VoxCPM2) ve `modal_deploy_xtts.py` (XTTS-v2 yedek motor)
2. **Kaggle / Google Colab Ücretsiz GPU (İkincil — T4 16 GB VRAM):** `notebooks/kaggle_colab_tts_server.ipynb` (`cloudflared` tüneli ile)
3. **Yerel / Docker Mock Sunucusu (Geliştirme ve CI/CD):** `uvicorn tts_server.local_main:app --port 8009` (`TTS_ENGINE=mock`, `TTS_ALLOW_MOCK=true`)

---

## 1. Modal.com Kurulumu (Önerilen: tek komut)

Ön koşul: [modal.com](https://modal.com) hesabı (GitHub/Google ile ücretsiz açılır; aylık ücretsiz kredi vardır).

```powershell
powershell -ExecutionPolicy Bypass -File scripts\kur-modal-tts.ps1
```

Betik sırasıyla: `gpu\.venv` içine Modal CLI kurar → (oturum yoksa) tarayıcıda giriş ister → `.env` içindeki `TTS_API_TOKEN` ile `seslikitap-tts-secret` oluşturur → **iki modeli** dağıtır (`openbmb/VoxCPM2` ve Türkçe LoRA'lı `Trendyol/Trendyol-TTS`) → URL'leri `.env`'e yazar (`TTS_MODAL_URL` = seçili motor, `TTS_MODAL_URL_BASE` / `TTS_MODAL_URL_TRENDYOL` = A/B için) → worker'ı yeniden başlatır → sağlık kontrolü yapar. İlk çalıştırmada imaj oluşturma ve model indirme 5–10 dakika sürebilir. Boştaki GPU 120 sn sonra kapanır (`scaledown_window`), kapalıyken ücret yoktur.

Yalnızca tek model için: `-Model base` veya `-Model trendyol`; aktif motoru seçmek için `-Active trendyol`.

### Kulak testi (A/B)

`referans-ses\ses.wav` + `referans-ses\ses.txt` koyun (bkz. `referans-ses\README.md`), sonra:

```powershell
src\SesliKitap.PythonWorker\.venv\Scripts\python.exe scripts\ses-karsilastirma.py
```

Aynı referans, aynı 6 cümle ve aynı tohumla her model için `referans-ses\sonuc\*.wav` üretir; ekranda tonal oran ve RTF (gerçek GPU hızı) yazar. Daha doğal olanı `.env` içindeki `TTS_MODAL_URL` değerine yazın (veya betiği `-Active` ile yeniden çalıştırın).

### Elle kurulum (betik kullanmadan)

```powershell
pip install modal; modal setup
modal secret create seslikitap-tts-secret TTS_API_TOKEN=<.env-deki-deger> --force
cd gpu; modal deploy modal_deploy.py      # temel model
$env:SK_TTS_MODEL_ID="Trendyol/Trendyol-TTS"; $env:SK_TTS_APP_SUFFIX="trendyol"; modal deploy modal_deploy.py
```

Komut çıktısındaki `https://...modal.run` adresini `.env` içindeki `TTS_MODAL_URL` ve `TTS_MODAL_HEALTH_URL` (`.../v1/health`) alanlarına yazın.

> **Not:** `requirements.txt` içinde `voxcpm==2.0.3` sabittir; eski sürümler referans sesi sessizce yok sayar ve sunucu bu durumda açılışta hata verir. Yerel mock sunucusu (bölüm 3) yalnızca API sözleşmesi testleri içindir; sinüs benzeri çıktısı worker'ın kalite denetiminde `tonal_signal` olarak reddedilir.

Yedek XTTS-v2 sunucusu (isteğe bağlı): `modal deploy gpu/modal_deploy_xtts.py`

---

## 2. Kaggle / Google Colab Kurulumu (`voxcpm2_alt`)

1. `gpu/notebooks/kaggle_colab_tts_server.ipynb` dosyasını Kaggle (Accelerator: **GPU T4 x2**) veya Google Colab (Runtime: **T4 GPU**) üzerinde açın.
2. İlk hücrede `TTS_AUTH_TOKEN` değerini belirleyin ve hücreleri sırayla çalıştırın.
3. Son hücrenin çıktısındaki `https://xxxx.trycloudflare.com` adresini `.env` dosyasındaki `TTS_ALT_URL` değişkenine yazın.

---

## 3. Yerel Duman Testi (Smoke Test) ve Birim Testler

### Birim Testler (GPU Gerekmez)
```powershell
$env:PYTHONPATH = "gpu"
.\src\SesliKitap.PythonWorker\.venv\Scripts\pytest.exe gpu/tests -v
```

### Duman Testi (`scripts/gpu-smoke-test.py`)
```powershell
python scripts/gpu-smoke-test.py --url http://localhost:8009 --token dev-tts-secret-token-change-me-32chars
```
