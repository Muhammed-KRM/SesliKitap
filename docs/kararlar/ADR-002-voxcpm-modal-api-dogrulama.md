# ADR-002: VoxCPM2 ve Modal.com Python SDK Güncel API Doğrulaması

- **Tarih:** 2026-10-24
- **Durum:** Kabul

## Bağlam

Geliştirici dökümanındaki **Kural 10 (API ve Kütüphane Doğrulama Zorunluluğu)** ve **Faz 3 Adım 3.1** uyarınca, `gpu/` paketini kodlamaya başlamadan önce `OpenBMB/VoxCPM` (`voxcpm` PyPI paketi) ve `Modal.com` Python SDK (`modal>=0.68`) imzalarının resmi kaynaklardan doğrulanması gerekmektedir.

## Doğrulanan API İmzaları

### 1. OpenBMB VoxCPM2 (`voxcpm` PyPI Paketi)

- **Resmi Depo ve Paket:** `https://github.com/OpenBMB/VoxCPM`, PyPI: `voxcpm>=0.1.0` (Python >=3.10, <3.13; PyTorch >=2.5.0; CUDA >=12.0).
- **Model Yükleme İmzası:**
  ```python
  from voxcpm import VoxCPM

  model = VoxCPM.from_pretrained(
      "openbmb/VoxCPM2",
      load_denoiser=False,
      device="cuda",  # "cuda" | "cpu" | "mps" | "auto"
  )
  sample_rate = getattr(getattr(model, "tts_model", model), "sample_rate", 48000)
  ```
- **Sentez (`model.generate`) İmzası ve Klonlama Modları:**
  1. **Sadece Referans Ses ile Sıfır-Atış Timbre Klonlama (`prompt_text` olmadan):**
     VoxCPM2, transkript metni olmadan doğrudan `reference_wav_path` ile konuşmacı ses rengini klonlayabilmektedir:
     ```python
     wav = model.generate(
         text=target_text,
         reference_wav_path=str(wav_path),
         cfg_value=2.0,
         inference_timesteps=10,
     )
     ```
  2. **Yüksek Sadakatli (Hi-Fi / Continuation) Klonlama (`prompt_text` ile):**
     Referans sesin birebir transkripti (`prompt_text`) mevcutsa hem `prompt_wav_path` + `prompt_text` hem de `reference_wav_path` birlikte verilerek en yüksek ses benzerliği elde edilir:
     ```python
     wav = model.generate(
         text=target_text,
         prompt_wav_path=str(wav_path),
         prompt_text=prompt_text,
         reference_wav_path=str(wav_path),
         cfg_value=2.0,
         inference_timesteps=10,
     )
     ```
  - **Önemli Kural:** `prompt_wav_path` verildiğinde `prompt_text` zorunludur; `prompt_text` boş/None ise yalnızca `reference_wav_path` gönderilmelidir (veya eski VoxCPM 1.x sürümleriyle uyumluluk için `inspect.signature(model.generate)` ile desteklenen parametreler çalışma zamanında kontrol edilmelidir).

### 2. Modal.com Python SDK (`modal>=0.68`)

- **Uygulama ve Konteyner Yaşam Döngüsü:**
  - `modal.App("seslikitap-tts-voxcpm2")`
  - `modal.Volume.from_name("seslikitap-tts-cache", create_if_missing=True)`
  - `@app.cls(gpu="L4", image=image, volumes={"/cache": volume}, secrets=[modal.Secret.from_name("seslikitap-tts-secret")], scaledown_window=300, timeout=600)`
  - `@modal.concurrent(max_inputs=1)` (GPU belleğinde aynı anda tek sentez isteği işleyerek OOM önleme)
  - `@modal.enter()` ile konteyner başlangıcında modeli `/cache/hf` ve `/cache/voices` dizinleriyle bir kez GPU belleğine yükleme
  - `@modal.asgi_app()` ile `tts_server.api.create_app(engine, voice_store)` FastAPI uygulamasını dış dünyaya açma

### 3. Yerel / Otomatik Test Ortamı Kararı (`tts-mock` Servisi)

- Üretimde ve GPU duman testinde Modal L4 (`voxcpm2_modal`) veya Kaggle/Colab (`voxcpm2_alt`) kullanılırken, yerel Docker geliştirme ortamında (`docker-compose.dev.yml`) etkileşimli Modal hesabı kurulumu beklenmeden tüm uçtan uca akışların (Faz 3 ses profili hazırlama + örnek ses üretimi ve Faz 4 tam bölüm sentezleme) kesintisiz doğrulanabilmesi için `gpu/` paketi içindeki `tts_server.local_main:app` (`TTS_ENGINE=mock`, `TTS_ALLOW_MOCK=true`) servisi `docker-compose.dev.yml` içine `tts-server` (`seslikitap-dev-tts`, port `8009`) olarak da eklenmiştir.
- Kullanıcı `.env` dosyasında `TTS_MODAL_URL` değerini kendi Modal URL'sine çevirdiğinde sistem hiçbir kod değişikliği olmadan gerçek L4 GPU üzerinde çalışır.

## Sonuçlar

- `gpu/tts_server/engines/voxcpm_engine.py` içinde hem `reference_wav_path` (VoxCPM2) hem de `prompt_wav_path` + `prompt_text` (Hi-Fi mod) `inspect.signature` korumasıyla desteklenecektir.
- `gpu/modal_deploy.py` ve `gpu/modal_deploy_xtts.py` güncel `scaledown_window=300`, `@modal.concurrent(max_inputs=1)` ve `@modal.asgi_app()` dekoratörleriyle yazılacaktır.
