# SesliKitap — Uzak GPU TTS Sunucusu (`gpu/`)

Bu dizin, SesliKitap Python Worker'ının HTTP üzerinden çağırdığı durumsuz (stateless) GPU TTS sunucusunu barındırır. Aynı `tts_server` çekirdek paketi üç farklı ortamda değişiklik yapılmadan çalışır:

1. **Modal.com Serverless GPU (Birincil — L4 24 GB VRAM):** `modal_deploy.py` (VoxCPM2) ve `modal_deploy_xtts.py` (XTTS-v2 yedek motor)
2. **Kaggle / Google Colab Ücretsiz GPU (İkincil — T4 16 GB VRAM):** `notebooks/kaggle_colab_tts_server.ipynb` (`cloudflared` tüneli ile)
3. **Yerel / Docker Mock Sunucusu (Geliştirme ve CI/CD):** `uvicorn tts_server.local_main:app --port 8009` (`TTS_ENGINE=mock`, `TTS_ALLOW_MOCK=true`)

---

## 1. Modal.com Kurulumu (Adım Adım)

1. Modal CLI paketini kurun ve hesabınıza giriş yapın:
   ```powershell
   pip install modal
   modal setup
   ```
2. TTS sunucusu için paylaşılan Bearer kimlik doğrulama anahtarını oluşturun:
   ```powershell
   modal secret create seslikitap-tts-secret TTS_AUTH_TOKEN=<guclu-rastgele-token>
   ```
   *(Aynı değeri kök dizindeki `.env` dosyasında `TTS_MODAL_AUTH_TOKEN` değişkenine yazın.)*
3. VoxCPM2 sunucusunu dağıtın:
   ```powershell
   modal deploy gpu/modal_deploy.py
   ```
4. Komut çıktısında verilen HTTPS adresini (ör. `https://<kullanici>--seslikitap-tts-voxcpm2-voxcpmserver-web.modal.run`) `.env` dosyasındaki `TTS_MODAL_URL` ve `TTS_MODAL_HEALTH_URL` (`.../v1/health`) alanlarına yapıştırın.
5. (İsteğe bağlı) Yedek XTTS-v2 sunucusunu dağıtmak için:
   ```powershell
   modal deploy gpu/modal_deploy_xtts.py
   ```

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
