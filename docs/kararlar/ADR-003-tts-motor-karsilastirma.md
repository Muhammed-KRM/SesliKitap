# ADR-003: Türkçe Sesli Kitap Üretimi İçin TTS Motorlarının Karşılaştırması

- **Tarih:** 2026-10-24
- **Durum:** Kabul

## Bağlam

Geliştirici dökümanı **Faz 3 Adım 3.1** kapsamında, Türkçe uzun metin (kitap/roman) seslendirmesi ve sıfır-atış (zero-shot) ses klonlama için mevcut açık kaynaklı TTS modelleri değerlendirilmiştir:
- **OpenBMB VoxCPM2 (2B)**
- **Coqui XTTS-v2**
- **F5-TTS**
- **Fish-Speech (1.5)**
- **Chatterbox**

## Karşılaştırma Tablosu

| Kriter | OpenBMB VoxCPM2 (2B) | Coqui XTTS-v2 | Fish-Speech 1.5 | F5-TTS | Chatterbox |
|---|---|---|---|---|---|
| **Türkçe Desteği** | Yerel (30 dil içinde resmi Türkçe desteği) | Yerel (17 dil içinde resmi Türkçe desteği) | Çok dilli (Türkçe verisi sınırlı, ince ayar gerektirir) | Topluluk modelleriyle sınırlı | Ağırlıklı İngilizce |
| **Çıkış Örnekleme Hızı** | **48 kHz** (stüdyo kalitesi) | 24 kHz | 44.1 kHz | 24 kHz | 24 kHz |
| **Mimari** | Tokenizer-free sürekli uzay difüzyon + AR (2B) | VQ-VAE + GPT-2 AR | Dual-AR + Firefly GAN | Flow Matching (DiT) | Llama omurgalı AR |
| **Ses Klonlama (Zero-Shot)** | 5–30 sn referans ses (`reference_wav_path` + opsiyonel `prompt_text`) | 6–15 sn referans ses (`speaker_wav`, `language="tr"`) | 10–30 sn referans ses | Referans ses + zorunlu transkript | 5–10 sn referans ses |
| **Prosodi ve Uzun Roman Okuma** | Bağlam duyarlı, nefes ve tonlama geçişleri çok doğal | İyi, ancak uzun cümlelerde bazen hızlanma/halüsinasyon | Kısa metinde iyi, Türkçe eklerde vurgu hatası olabiliyor | Akıcı ancak Türkçe fonetik uyumu zayıf | İngilizce dışı dillerde aksanlı |
| **L4 GPU (24 GB) RTF** | ~0.15–0.22x (gerçek zamandan ~5x hızlı) | ~0.12–0.18x (gerçek zamandan ~6x hızlı) | ~0.20x | ~0.15x | ~0.25x |
| **Lisans** | **Apache-2.0** | Coqui Public Model License (CPML) | CC-BY-NC-SA 4.0 | MIT | MIT |

## Karar

1. **Birincil Motor (`voxcpm2_modal` ve `voxcpm2_alt`):** Resmi Türkçe desteği, 48 kHz yerel örnekleme hızı, Apache-2.0 lisansı ve tokenizer-free mimarisinin Türkçe sondan eklemeli uzun kelimelerde hece yutmaması nedeniyle **OpenBMB VoxCPM2** birincil motor olarak doğrulanmıştır.
2. **İkincil / Yedek Model Ailesi (`xtts2_modal`):** Resmi Türkçe (`language="tr"`) desteği ve olgunlaşmış çok dilli klonlama başarımı nedeniyle **Coqui XTTS-v2** yedek model ailesi olarak korunmuştur.
3. **Diğer Modeller (F5-TTS, Fish-Speech, Chatterbox):** Resmi sıfır-atış Türkçe doğrulukları VoxCPM2 ve XTTS-v2 seviyesinde olmadığından üretim zincirine dahil edilmemiştir; ancak `BaseTtsServerEngine` arayüzü sayesinde ileride tek bir sınıf eklenerek sisteme entegre edilebilir.
