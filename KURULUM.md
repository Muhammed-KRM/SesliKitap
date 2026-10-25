# 📖 SesliKitap — Sıfırdan Yeni Bilgisayara Kurulum Rehberi

Bu rehber, **SesliKitap** projesini sıfır veya başka bir bilgisayara kurarken hiçbir şeyi unutmadan, eksiksiz ve dakikalar içinde çalışır hale getirmeniz için adım adım hazırlanmıştır.

---

## 📋 1. Yeni Bilgisayarda Gereken Ön Yazılımlar

Projeyi çalıştırmadan önce yeni bilgisayara şu ücretsiz araçların kurulu olduğundan emin olun:

1. **Git:** [git-scm.com](https://git-scm.com/) (Projeyi bilgisayara indirmek için)
2. **Docker Desktop:** [docker.com](https://www.docker.com/products/docker-desktop/) (Veritabanı, Redis, RabbitMQ, MinIO ve ses işleme worker'ı için şarttır. Kurduktan sonra açıp çalışır durumda bırakın).
3. **.NET 10 SDK:** [dotnet.microsoft.com](https://dotnet.microsoft.com/download) (Backend C# API için).
4. **Node.js (v20 veya v22 LTS):** [nodejs.org](https://nodejs.org/) (Angular arayüzü için).
5. **Python 3.11 veya 3.12:** [python.org](https://www.python.org/) (Yerel testler ve scriptler için).

---

## 🔑 2. En Çok Merak Edilen: "Config, .env ve API Anahtarları Ne Olacak?"

Git depolarında güvenlik nedeniyle `.env` dosyaları gizlenir (şifrelerinizin çalınmaması için). Ancak projede sizin için tüm ayarları **hazır ve çalışan değerlerle** içeren bir [.env.example](file:///.env.example) dosyası bulunmaktadır.

Yeni bilgisayarda yapmanız gereken tek şey, terminalde proje klasöründeyken şu komutu çalıştırmaktır:

```powershell
Copy-Item .env.example .env
```

*(Linux/Mac kullanıyorsanız: `cp .env.example .env`)*

### `.env` İçindeki Kritik Bilgiler Nereden Geliyor?
* **Modal TTS URL & Token (`TTS_MODAL_URL`, `TTS_API_TOKEN`):**
  * Modal.com üzerindeki L4 GPU sunucunuz **bulutta bağımsız olarak yaşar**.
  * Yeni bilgisayarda olsanız bile buluttaki sunucunuz (`https://ustunmuhammed09--seslikitap-tts-voxcpm2-trendyol-voxcpms-edce4b.us-east.modal.run`) **aynı URL ve aynı token ile doğrudan çalışır!** Yeni bilgisayarda tekrar model yüklemenize veya Modal kurmanıza gerek yoktur.
* **Veritabanı ve Şifreler:**
  * Docker içindeki PostgreSQL, Redis, RabbitMQ ve MinIO şifreleri `.env.example` içinde zaten birbirine uyumlu şekilde ayarlanmıştır (`postgres`, `minioadmin`, `guest`). Hiçbirini değiştirmenize gerek yoktur.
* **Arayüz Erişim Anahtarı (`ADMIN_API_KEY`):**
  * `.env` içindeki `ADMIN_API_KEY` değeri:
    `I3PFUCmsvNqOy2dXvlWi-bOQp84Amh4SyvNizZb5trLl0MBeiDSXrF2QMaTd7ErL`
  * Tarayıcı ilk açıldığında erişim şifresi sorarsa bu anahtarı yapıştırmanız yeterlidir.

---

## 🚀 3. Adım Adım Kurulum ve Çalıştırma

Terminalinizi (**PowerShell**) açın ve sırasıyla şu adımları uygulayın:

### Adım 1: Projeyi Klonlayın
```powershell
git clone <sizin-repo-adresiniz> Seslikitap
cd Seslikitap
```

### Adım 2: `.env` Dosyasını Oluşturun
```powershell
Copy-Item .env.example .env
```

### Adım 3: Docker Servislerini Ayağa Kaldırın
Docker Desktop'ın açık olduğundan emin olun ve şu komutu verin:
```powershell
docker compose -f docker-compose.dev.yml up -d
```
> Bu komut Postgres (veritabanı), Redis (önbellek), RabbitMQ (kuyruk), MinIO (ses depolama) ve SesliKitap Python Worker konteynerlerini otomatik olarak başlatır.

### Adım 4: Backend API'yi Başlatın
Ayrı bir terminal penceresinde:
```powershell
dotnet run --project src/SesliKitap.API --launch-profile http
```
> API `http://localhost:5201` portunda çalışmaya başlar ve veritabanı tablolarını otomatik olarak günceller.

### Adım 5: Hazır Ses Profillerini Yükleyin (Tek Komut)
Yeni açılan bir veritabanında ses profillerinin (Ahmet Bey, Murat, Zeynep Hanım, Aylin) hemen görünmesi için hazırladığımız hazır ses yükleyicisini çalıştırın:
```powershell
.\scripts\seed-hazir-sesler.ps1
```
> Bu script `referans-ses/hazir_sesler` klasöründeki temiz stüdyo seslerini yerel MinIO ve veritabanınıza otomatik olarak aktarır.

### Adım 6: Web Arayüzünü Başlatın
Ayrı bir terminal penceresinde:
```powershell
cd src/SesliKitap.Frontend
npm install
npm start
```
> Tarayıcınızda **`http://localhost:4300`** adresine gidin. Projeniz tüm özellikleriyle, kitaplarınızla ve seslerinizle kullanıma hazırdır! 🎉

---

## ⚡ 4. Kolay Başlatma (Tek Tıkla Çalıştırma Betiği)

Her defasında 3 farklı terminal açmak yerine proje kök dizinindeki hazır başlatma betiğini kullanabilirsiniz:

```powershell
.\baslat.ps1
```

Bu script sırasıyla Docker konteynerlerini kontrol eder, Backend API'yi ve Frontend arayüzünü sizin için otomatik olarak ayağa kaldırır.

---

## 🛠️ 5. Sık Karşılaşılan Durumlar ve Çözümleri

1. **"Port zaten kullanımda" Hatası:**
   * Bilgisayarınızda yerel PostgreSQL (5432) kuruluysa bile sistem çakışmasın diye projemiz **5433** portunu kullanır.
   * MinIO `9010`, Redis `6380`, RabbitMQ `5673`, API `5201`, Arayüz `4300` portundadır. Bu portların boş olduğundan emin olun.
2. **İlk Sentezde 20–30 Saniye Bekleme (Cold Start):**
   * Modal L4 GPU sunucusu maliyet tasarrufu için boştayken uyku moduna geçer.
   * İlk bölümü veya ses örneğini üretirken GPU'nun uyanması ~20 saniye sürebilir, ardından gelen tüm bölümler saniyeler içinde yıldırım hızında üretilir.
3. **Modal Sunucusunu Sıfırdan Tekrar Dağıtmak İsterseniz:**
   * Buluttaki sunucu zaten çalışmaktadır, ancak sıfırdan başka bir Modal hesabına kurmak isterseniz:
     ```powershell
     cd gpu
     modal setup
     modal deploy modal_deploy.py
     ```
     çıkan yeni web URL'sini `.env` dosyasındaki `TTS_MODAL_URL` karşısına yapıştırmanız yeterlidir.
