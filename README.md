# SesliKitap

**SesliKitap**, Türkçe ve çok dilli e-kitapları (`PDF`, `EPUB`, `TXT`) akıllı metin temizleme, bölüm tespiti, Türkçe metin normalizasyonu ve sunucusuz GPU üzerinde **VoxCPM2** (ve yedekte **XTTS-v2**) ses klonlama teknolojisiyle doğal insan sesine dönüştüren uçtan uca bir sesli kitap üretim ve dinleme platformudur.

## Temel Özellikler

- **Akıllı Kitap İşleme:** Üst/alt bilgi, sayfa numarası ve dipnot temizliği; otomatik bölüm tespiti; etkileşimli bölüm önizleme ve düzenleme.
- **Gerçekçi İnsan Sesi ve Ses Klonlama:** 10–30 saniyelik referans ses kaydıyla kendi sesinizi veya seçtiğiniz anlatıcı sesini klonlayarak (`VoxCPM2` / `Modal.com` Serverless GPU) duygulu ve akıcı okuma.
- **Storytel Benzeri Modern PWA Oynatıcı:** Angular 18 tabanlı karanlık/açık temalı web ve mobil PWA; cümle-ses senkron vurgusu (karaoke modu), uyku zamanlayıcısı, hız kontrolü, çevrimdışı dinleme (IndexedDB) ve cihazlar arası anlık konum senkronizasyonu.
- **Esnek Dışa Aktarım:** Bölüm bazlı `Opus` / `MP3` akışı, `ZIP` arşivi ve bölüm işaretli tek dosya `.m4b` (Apple Books / SmartAudioBook Player uyumlu) indirme.
- **Açık Kaynak Kitap Keşfi:** Internet Archive, Open Library ve Project Gutenberg üzerinden doğrudan kitap arama ve tek tıkla kütüphaneye aktarma.

## Mimari ve Geliştirici Dokümanı

Projenin tüm mimari kararları, katmanlı yapısı (`.NET 10`, `Python 3.11 Worker`, `Angular 18 PWA`, `PostgreSQL`, `Redis`, `RabbitMQ`, `MinIO`, `Modal Serverless GPU`), veri modelleri ve faz bazlı geliştirme rehberi aşağıdaki ana belgede ayrıntılı olarak açıklanmıştır:

- [Geliştirici Dokümanı ve Mimari Anayasa](docs/gelistirici-dokumani.md)
- [Mimari Karar Kayıtları (ADR)](docs/kararlar/)

> **Not:** Tam kurulum, üretim dağıtımı ve kullanım kılavuzu **Faz 9** sonunda bu dosyaya eklenecektir. Geliştirme ortamı kurulumu için `docs/gelistirici-dokumani.md` içindeki Faz 1–9 adımlarını takip edebilirsiniz.
