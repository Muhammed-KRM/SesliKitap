# 🔄 SesliKitap — Bilgisayarlar Arası Geçiş ve Kaldığı Yerden Devam Etme Rehberi

Bu rehber, bir bilgisayarda (Bilgisayar A) seslendirilmeye başlanmış ve bir kısmı tamamlanmış (örneğin 19 bölümü bitmiş) bir kitabı, **başka bir bilgisayara (Bilgisayar B) aktararak hiçbir veri veya ses kaybı olmadan tam kaldığı bölümden (21. bölümden) devam ettirmek** için hazırlanmıştır.

---

## 📦 1. Bilgisayar A'dan Çıkmadan Önce Alınması Gerekenler (Kontrol Listesi)

Bir flash belleğe veya Google Drive / bulut deponuza şu dosyaları koyduğunuzdan emin olun:

| Dosya / Klasör | Nereden Alınacak? | Boyut | Açıklama |
| :--- | :--- | :--- | :--- |
| **`seslikitap_backup_*.zip`** | `D:\Seslikitap\backups\` | ~290 MB | **EN KRİTİK DOSYA.** Veritabanı (`db.dump`), üretilen tüm MP3/Opus sesler, kitap metinleri ve kapakları barındırır. |
| **`.env`** | `D:\Seslikitap\.env` | ~2 KB | Modal GPU adresi (`TTS_MODAL_URL`), API token ve bağlantı şifreleri. Git'e atılmadığı için yanınızda almanız işinizi çok kolaylaştırır. |
| *(İsteğe Bağlı)* **Yükleyiciler** | İndirilenler | ~1-2 GB | Eğer Bilgisayar B'de internet yavaşsa: Docker Desktop Installer, .NET SDK ve Node.js kurulum exe'lerini şimdiden flash belleğe atabilirsiniz. |

> [!TIP]
> **Önceden İndirdiğiniz Opus Sesleri:** Opus dosyalarını ayrıca Drive'a indirmiş olmanız harika bir yedek önlemdir. Ancak sistemin bölümleri "tamamlandı" olarak algılayıp kaldığı yerden devam etmesi için asıl gereken yukarıdaki **`.zip`** yedek paketidir.

---

## 📤 2. Bilgisayar A'da Yapılacak Son İşlemler (Yedek Alma)

1. **Çalışan İşlemi Durdurun:**
   * Tarayıcınızda (`http://localhost:4300`) açık olan kitap sayfasında **"İşi İptal Et"** butonuna basın. Böylece o an sentezlenen parça temiz bir şekilde sonlanır ve veritabanı kararlı duruma geçer. (Daha önce tamamlanmış 19 bölüm asla silinmez).
2. **Yedekleme Betiğini Çalıştırın:**
   * Proje kök dizininde (`D:\Seslikitap`) PowerShell açın:
     ```powershell
     .\scripts\backup.ps1
     ```
   * Betik otomatik olarak:
     * PostgreSQL veritabanını dışa aktarır (`db.dump`).
     * MinIO depolama alanındaki üretilmiş tüm sesleri toplar (geçici FLAC önbelleklerini hariç tutarak boyutu ~290 MB'a indirir).
     * `backups/seslikitap_backup_<tarih_saat>.zip` arşivini oluşturur.
3. Oluşan bu `.zip` dosyasını ve ana dizindeki `.env` dosyasını flash belleğinize veya Google Drive'a kopyalayın.

---

## 📥 3. Bilgisayar B'de Yapılacak Kurulum ve Geri Yükleme

### Adım 1: Ön Yazılımları Doğrulayın
Bilgisayar B'de şu yazılımların kurulu olduğundan emin olun (Ayrıntılar için [KURULUM.md](file:///d:/Seslikitap/KURULUM.md) dosyasına bakabilirsiniz):
1. **Docker Desktop** (Kurulu ve çalışıyor olmalıdır)
2. **Git**
3. **.NET SDK (8 veya 10)**
4. **Node.js (v20+ LTS)**

### Adım 2: Projeyi Bilgisayar B'ye Alın
* **Seçenek A (Git ile):**
  ```powershell
  git clone https://github.com/Muhammed-KRM/SesliKitap.git
  cd SesliKitap
  ```
  *(Angular paketleri için: `cd src/SesliKitap.Frontend; npm install; cd ../..`)*
* **Seçenek B (Tüm Klasörü Kopyalayarak):**
  * Bilgisayar A'daki `D:\Seslikitap` klasörünü doğrudan harici diskle Bilgisayar B'ye kopyalarsanız hiçbir paket veya bağımlılık indirmek zorunda kalmazsınız.

### Adım 3: Dosyaları Yerleştirin
* Bilgisayar A'dan getirdiğiniz **`.env`** dosyasını projenin en üst dizinine (`SesliKitap/.env`) yapıştırın.
* Bilgisayar A'dan getirdiğiniz **`seslikitap_backup_*.zip`** dosyasını `SesliKitap/backups/` klasörüne yapıştırın (yoksa klasörü oluşturun).

### Adım 4: Docker Servislerini Başlatın
PowerShell'de proje klasöründeyken:
```powershell
docker compose -f docker-compose.dev.yml up -d
```
*(Tüm altyapı servisleri: PostgreSQL, Redis, RabbitMQ, MinIO ve Worker arka planda ayağa kalkar).*

### Adım 5: Tek Komutla Verileri Geri Yükleyin
PowerShell'de şu komutu çalıştırın (Dosya adını kendi zip adınıza göre güncelleyin):
```powershell
.\scripts\restore.ps1 -BackupPath .\backups\seslikitap_backup_20261026_172320.zip -Force
```

Bu işlem birkaç saniye içinde:
* Tüm veritabanını (kitaplar, bölümler, temizlenmiş metinler, süreler) geri yükler.
* Tüm üretilmiş sesleri MinIO nesne deposuna yerleştirir.
* Worker servisini yeni verilerle senkronize etmek için yeniden başlatır.

---

## 🚀 4. Projeyi Başlatma ve Kaldığı Yerden Devam Ettirme

1. **Projeyi Başlatın:**
   ```powershell
   .\baslat.ps1
   ```
2. **Tarayıcıyı Açın:**
   * `http://localhost:4300` adresine gidin.
   * Şifre sorarsa `.env` içindeki `ADMIN_API_KEY` değerini girin.
3. **Kitabı Kontrol Edin:**
   * "Kitaplar" menüsünden **"Oyun Tasarımı"** kitabını açın.
   * İlk 19-20 bölümün **Yeşil renkte (Hazır)** olduğunu ve her birinin yanında oynatma (Play) butonu olduğunu göreceksiniz.
4. **Kaldığı Yerden Devam Ettirin:**
   * Sağ üstteki **"Seslendir"** butonuna tıklayın.
   * Açılan pencerede varsayılan ses profili ve kapsam:
     * **Kapsam:** *"Yalnızca eksik veya metni değişmiş bölümler"* (Default)
   * **"Başlat"** butonuna basın.
5. **Sonuç:**
   * Sistem daha önce üretilen 19-20 bölümü anında tespit eder ve atlar.
   * Sentezleme işlemi doğrudan **21. Bölümden** başlar!
   * Modal GPU bulutta (`TTS_MODAL_URL`) bağımsız çalıştığı için, Bilgisayar B'nin donanımı ne olursa olsun aynı yüksek hız ve kalitede seslendirme devam eder.

---

## ❓ Sıkça Sorulan Sorular

### S1: Modal GPU'yu Bilgisayar B'de tekrar kurmam veya deploy etmem gerekir mi?
**Hayır.** Modal GPU sunucusu bulut üzerinde (serverless NVIDIA L4 GPU) 7/24 hazır beklemektedir. `.env` dosyasındaki `TTS_MODAL_URL` ve `TTS_API_TOKEN` Bilgisayar B'de mevcut olduğu sürece başka hiçbir GPU ayarı gerekmez.

### S2: Biten bölümler tekrar seslendirilir mi ve ekstra kota harcar mı?
**Hayır.** Geri yükleme sonrasında veritabanında bu bölümlerin durumu `3` (Hazır) olarak görünür ve `ses_metin_surumu == metin_surumu` eşleştiği için sistem onları yok sayarak doğrudan eksik olan ilk bölüme geçer.

### S3: Bilgisayar B'de port çakışması olur mu?
Hayır. Proje portları `5201` (API), `4300` (Web), `5434` (DB), `6381` (Redis), `5674` (RabbitMQ) ve `9010` (MinIO) olarak standart çakışmaları engelleyecek şekilde sabitlenmiştir.
