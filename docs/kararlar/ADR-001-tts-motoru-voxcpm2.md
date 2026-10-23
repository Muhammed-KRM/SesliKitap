# ADR-001: TTS Motoru Olarak VoxCPM2 ve Sunucusuz GPU (Modal.com) Seçimi

- **Tarih:** 2026-10-23
- **Durum:** Kabul

## Bağlam

SesliKitap projesinin temel hedefi, kitapları mekanik ekran okuyucu tonunda değil, gerçek bir seslendirme sanatçısı veya kullanıcının kendi klonlanmış sesiyle, duygulu ve doğal vurgu/tonlama (prosodi) ile okumaktır.

Mevcut donanım ve maliyet kısıtları:
1. Geliştirme bilgisayarlarında **RTX 3050 Ti (4 GB VRAM)** ve **GTX 1650/1660 (4–6 GB VRAM)** bulunmaktadır.
2. Yüksek kaliteli modern konuşma modelleri (2B+ parametre) 8–12 GB VRAM gerektirdiğinden yerel makinelerde çalışamamakta veya CPU'da kabul edilemez derecede yavaş kalmaktadır.
3. 7/24 açık kiralık bir GPU sunucusu (aylık 150–400 USD) kişisel kullanım için ekonomik değildir; yalnızca kitap seslendirilirken saniye bazlı çalışan ve iş bitince sıfıra inen (`scale-to-zero`) bir yapı gerekmektedir.

## Karar

1. **Birincil TTS Motoru:** Açık kaynaklı (Apache-2.0) **OpenBMB VoxCPM2** (2B parametre, 48 kHz yerel çıkış, bağlam duyarlı prosodi, sıfır-atış ses klonlama ve Türkçe dahil 30 dil desteği) seçilmiştir.
2. **Çalıştırma Ortamı:** **Modal.com Serverless GPU (NVIDIA L4 24 GB VRAM)** üzerinde barındırılacak, `scaledown_window=300` ile iş bitiminden 5 dakika sonra otomatik kapanacak ve Modal Starter planındaki **aylık 30 USD ücretsiz kredi** ile ayda ~15–20 adet 200 sayfalı kitabı sıfır maliyetle seslendirecektir.
3. **Yedek Motor ve Ortam Zinciri (`TtsEngineChain`):**
   - Birincil: `voxcpm2_modal` (Modal L4 üzerinde VoxCPM2)
   - İkincil (Alternatif barındırma): `voxcpm2_alt` (Kaggle / Google Colab + `cloudflared` tüneli veya RunPod)
   - Üçüncül (Farklı model ailesi): `xtts2_modal` (Coqui **XTTS-v2** — çok dilli ses klonlama)
   - Test motoru: `mock` (Geliştirme ve CI/CD testlerinde sıfır GPU maliyeti için deterministik ses üreteci)
4. **Ses Tutarlılığı Kuralı:** Aynı kitabın bölümleri arasında ses rengi farkı oluşmaması için sentez işi başladıktan sonra model ailesi (`voxcpm2` ↔ `xtts2`) otomatik olarak değiştirilmez; yalnızca aynı model ailesinin alternatif uç noktasına (`voxcpm2_modal` → `voxcpm2_alt`) geçilir.

## Sonuçlar (Artı / Eksi)

- **Artılar:**
  - Yerel 4 GB VRAM darboğazına takılmadan en üst seviye açık kaynak ses klonlama ve doğal prosodi kalitesi elde edilir.
  - 200 sayfalık (~50.000 kelime, ~6 saat ses) bir kitap L4 GPU üzerinde ~1–1.2 saatte tamamlanır (~0.85–1.10 USD kredi tüketimi; aylık 30 USD ücretsiz kredi içinde bedava).
  - Parça bazlı içerik adresli önbellek (`ChunkCache`) sayesinde yarıda kalan veya güncellenen bölümlerde aynı cümleler tekrar GPU'ya gönderilmez.
- **Eksiler ve Riskler:**
  - İlk istekte ~60–120 saniye soğuk başlatma (`cold start`) gecikmesi yaşanır (Modal Volume üzerinde model ağırlıkları önbelleklenerek minimize edilir).
  - Bulut bağlantısı gerektirir (ancak üretilen sesler MinIO ve PWA IndexedDB'ye kaydedildiği için dinleme tamamen çevrimdışı yapılabilir).

## Alternatifler

1. **Microsoft Edge-TTS / Azure Neural TTS:** Ücretsiz/ucuz ve hızlıdır ancak ses klonlama yoktur, yalnızca sabit fabrika sesleri sunar ve uzun roman okumalarında haber spikeri gibi tekdüze kalır. Elendi.
2. **ElevenLabs / OpenAI TTS API:** Kalitesi yüksektir ancak 200 sayfalık tek bir kitap (~350.000 karakter) ElevenLabs'te ~35–60 USD, OpenAI TTS'te ~5.25–10.50 USD tutmaktadır. Sürekli kullanımda çok pahalıdır. Elendi.
3. **Yerel XTTS-v2 / Piper (RTX 3050 Ti 4 GB VRAM üzerinde):** Piper çok hızlıdır fakat ses klonlama ve prosodi kalitesi VoxCPM2 seviyesinde değildir; XTTS-v2 ise 4 GB VRAM'de sık sık OOM hatası vermekte veya çok yavaş çalışmaktadır. XTTS-v2 bulut üzerinde yedek motor olarak konumlandırılmıştır.
