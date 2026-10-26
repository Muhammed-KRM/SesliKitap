# ==============================================================================
# SesliKitap Tek Tıkla Başlatma Betiği (baslat.ps1)
# ==============================================================================

$ErrorActionPreference = "Continue"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "       SesliKitap Projesi Başlatılıyor...          " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. .env kontrolü
if (-not (Test-Path ".env")) {
    if (Test-Path ".env.example") {
        Write-Host "[1/4] .env bulunamadı, .env.example üzerinden kopyalanıyor..." -ForegroundColor Yellow
        Copy-Item .env.example .env
    } else {
        Write-Error ".env dosyası bulunamadı! Lütfen .env dosyasını oluşturun."
        exit 1
    }
}

# 2. Docker servislerini başlat
Write-Host "[2/4] Docker altyapı servisleri başlatılıyor..." -ForegroundColor Cyan
docker compose -f docker-compose.dev.yml up -d
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Docker başlatılırken bir uyarı oluştu. Docker Desktop'ın açık olduğundan emin olun."
}

# 3. Backend API'yi ayrı bir pencerede başlat
Write-Host "[3/4] SesliKitap .NET API başlatılıyor (Port 5201)..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot'; Write-Host 'SesliKitap API Çalışıyor (http://localhost:5201)...' -ForegroundColor Green; dotnet run --project src/SesliKitap.API --launch-profile http"

# 4. Frontend Angular'ı ayrı bir pencerede başlat
Write-Host "[4/4] SesliKitap Frontend başlatılıyor (Port 4300)..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot\src\SesliKitap.Frontend'; Write-Host 'Frontend Sunucusu Başlatılıyor (http://localhost:4300)...' -ForegroundColor Green; npm.cmd start"

Write-Host "==================================================" -ForegroundColor Green
Write-Host "Tüm servisler başlatıldı!                         " -ForegroundColor Green
Write-Host "Tarayıcınızda açın: http://localhost:4300         " -ForegroundColor Yellow
Write-Host "==================================================" -ForegroundColor Green
