# ADR-006: Gerçek motor zorunluluğu, sessiz sahte-ses geri dönüşünün yasaklanması ve ton (sinüs) tespiti

- **Tarih:** 2026-10-25
- **Durum:** Kabul

## Bağlam

Geliştirme sırasında Modal.com GPU sunucusu (VoxCPM2) dağıtılmadığı için worker, `TTS_ENGINE_ORDER` sonundaki yerel `mock` motoruna düşüyordu. `mock` motoru, geliştirici dokümanında belirtildiği gibi "metin uzunluğu × 60 ms sinüs" üretir. Sonuçlar:

1. Kullanıcı seslendirilmiş kitapta insan sesi yerine **"vuvuvuv"** (4 Hz zarflı sinüs) duydu.
2. Bunu "çözmek" için `mock` motoruna Google Çeviri TTS'i (gTTS) bağlandı; bu bir **dokümandan sapmaydı** ve (a) robotik ses ürettiği, (b) ses profilini yok saydığı, (c) toplu kullanımda Google'ın `429 Too Many Requests` engeline takıldığı için başarısız oldu. 429 durumunda kod **sessizce sinüse geri döndü**; 27.435 parça sinüsle üretildi ve 34.766 parça "gTTS" etiketiyle **önbelleğe** yazıldı.
3. `QualityChecker` yalnızca süre/seviye/kırpılma/sessizlik denetlediği için sinüsü "kaliteli" saydı (puan 1.0).
4. `docker-compose.dev.yml` içinde `ALLOW_MOCK_TTS: "true"` ve `...,mock` sırası sabitti; `.env` değeri bunu geçersiz kılıyordu.

## Karar

1. **gTTS tamamen kaldırıldı.** `mock` motoru yalnızca birim/E2E testleri içindir ve geniş bantlı gürültü üretir (saf ton değildir).
2. **Geliştirme ve üretimde `ALLOW_MOCK_TTS=false`**, `mock` `TTS_ENGINE_ORDER`'dan çıkarıldı (E2E compose dosyası testler için `mock` kullanmaya devam eder). Gerçek motor yoksa bölüm sahte sesle üretilmez; `TTS_ENGINE_UNAVAILABLE` hatasıyla iş **ilk bölümde durur**.
3. `QualityChecker`'a **tonal sinyal tespiti** eklendi: 1 sn'lik pencerelerde en güçlü 6 FFT binine düşen enerjinin medyanı `> 0.75` ise `tonal_signal` (ölçüm: gerçek konuşma 0.2–0.5, sinüs ≈ 0.89). Tonal çıktı yeniden denenir; yine tonal ise bölüm `CHUNK_NOT_SPEECH` ile **başarısız** olur ve parça önbelleğe yazılmaz.
4. `gpu/requirements.txt` `voxcpm==2.0.3` olarak sabitlendi (eski `>=0.1.0` pini referans sesi hata vermeden yok sayabilirdi); `VoxCpmEngine.load()` klonlamayı desteklemeyen sürümde **hata verir**.
5. Modal dağıtımı iki modelle yapılabilir (A/B kulak testi): `openbmb/VoxCPM2` (Apache-2.0) ve `Trendyol/Trendyol-TTS` (VoxCPM2 + Türkçe LoRA, MIT). Karar, aynı referans ve aynı cümlelerle yapılan dinleme testine göre verilir (`scripts/ses-karsilastirma.py`).

## Sonuçlar (Artı / Eksi)

- **Artılar:**
  - Sessiz kalite çöküşü imkânsız hale geldi; hata açıkça görünür.
  - Ton sinyali hiçbir koşulda "bölüm hazır" olmaz ve önbelleği zehirlemez.
  - Doğal ses için tek yol (gerçek VoxCPM2) net.
- **Eksiler ve Riskler:**
  - Modal hesabı ve GPU dağıtımı olmadan seslendirme yapılamaz (bilinçli).
  - Tonal eşik (0.75) sınırlı veriyle ayarlandı; gerçek motor çıktılarıyla A/B testinde doğrulanmalı.
  - Gerçek maliyet dokümandaki tahminden yüksek olabilir: L4'te RTF 0.2 yerine ~0.5–0.9 beklenir (25 saatlik kitap için ≈ 10–19 USD). Bu tahmin **doğrulanmamıştır**; ölçüm A/B testinde yapılır.

## Alternatifler

- **gTTS / Edge-TTS ile devam:** resmi olmayan uç noktalar, oran sınırı, ses klonlama yok, robotik → elendi.
- **Azure Speech / Google Chirp 3 HD:** doğal ve kararlı, ancak ses klonlamıyor veya onay gerektiriyor; ücretsiz kota sonrası ücretli → yedek seçenek olarak saklandı.
- **ElevenLabs:** en yüksek kalite, ancak 25 saatlik kitap için ≈ 250–330 USD → bütçe dışı.
- **Chatterbox Multilingual (MIT):** Türkçe + klonlama destekli hafif alternatif → VoxCPM2 yetersiz kalırsa ikinci aday.
