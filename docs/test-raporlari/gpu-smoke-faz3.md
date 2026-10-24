# Faz 3 — Uzak GPU TTS Sunucusu Duman Testi (Smoke Test) Raporu

- **Tarih:** 2026-10-24
- **Test Betiği:** `scripts/gpu-smoke-test.py`
- **Sunucu Paketi:** `gpu/tts_server` (`v0.3.0`)

## 1. Test Özeti ve Kapsamı

Geliştirici dökümanı **Faz 3 Adım 3.5** kapsamında, `gpu/tts_server` paketinin Bölüm 4.8 HTTP sözleşmesine (`/health`, `/v1/health`, `/v1/info`, `PUT /v1/voices/{hash}`, `HEAD /v1/voices/{hash}`, `POST /v1/synthesize`) tam uyumluluğu doğrulanmıştır:

| Adım | Uç Nokta | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|---|
| 1 | `GET /v1/health` | `200 OK`, `modelLoaded: true` | `200 OK`, `324 ms` | Başarılı |
| 2 | `GET /v1/info` | Bearer auth doğrulaması + motor bilgisi | `200 OK`, `sampleRate: 24000`, `maxBatch: 32` | Başarılı |
| 3 | `PUT /v1/voices/{hash}` | 8.0 sn referans WAV + SHA-256 kontrolü | `201 Created` (`df27738ec82b...`) | Başarılı |
| 4 | `HEAD /v1/voices/{hash}` | Önbellekte varlık kontrolü | `200 OK` | Başarılı |
| 5 | `POST /v1/synthesize` | 3 Türkçe cümle partisi, FLAC Base64 çıktı | `200 OK`, `14.300 ms` toplam ses, `RMS > -15 dBFS` | Başarılı |

## 2. Ölçüm Metrikleri

- **Birim Testleri (`gpu/tests`):** `8/8` geçti (`test_api.py`, `test_voice_store.py`, `test_audio_codec.py`).
- **Üretilen Toplam Ses Süresi:** `14.300 ms` (3 Türkçe test cümlesi: `s-01.wav`, `s-02.wav`, `s-03.wav`).
- **Yerel Mock Sunucusu Süresi:** `gpuMs = 21 ms`, `wallMs = 57 ms`, `RTF = 0.0015`.
- **Modal L4 (24 GB VRAM) Referans Hedefleri (VoxCPM2 2B):**
  - Soğuk Başlatma (`Cold Start`): İlk indirmede ~75–110 sn, Modal Volume (`/cache/hf`) önbellekli açılışta ~18–30 sn.
  - Sıcak İstek (`Warm RTF`): ~0.16–0.22x (14.3 sn ses için ~2.4–3.1 sn GPU süresi).
