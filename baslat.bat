@echo off
cls
echo ========================================================
echo       SesliKitap Sistemi Baslatiliyor...
echo ========================================================
echo.

REM Angular CLI Analytics istemini kapat
set "NG_CLI_ANALYTICS=false"

echo [1/4] Eski acik servisler temizleniyor (Eger varsa)...
taskkill /F /IM "SesliKitap.API.exe" /T > nul 2>&1
taskkill /FI "WINDOWTITLE eq SesliKitap-*" /F /T > nul 2>&1

REM Eger .env dosyasi yoksa generate-secrets.ps1 ile olustur
if not exist "%~dp0.env" (
    echo [.env] .env dosyasi bulunamadi, rastgele gizli anahtarlarla olusturuluyor...
    powershell -ExecutionPolicy Bypass -File "%~dp0scripts\generate-secrets.ps1" -WriteEnv
)

REM Eger .env dosyasi varsa ortama aktar
if exist "%~dp0.env" (
    echo [.env] Ortam degiskenleri yukleniyor...
    for /f "usebackq tokens=1* delims==" %%a in (`findstr /v "^#" "%~dp0.env"`) do (
        if not "%%a"=="" set "%%a=%%b"
    )
)
echo.

echo [2/4] Docker servisleri baslatiliyor (PostgreSQL, Redis, RabbitMQ, MinIO)...
docker compose -f "%~dp0docker-compose.dev.yml" up -d --wait
if %errorlevel% neq 0 (
    echo [HATA] Docker servisleri baslatilamadi! Docker Desktop calisiyor mu?
    if not "%~1"=="--no-pause" pause
    exit /b %errorlevel%
)

echo.
echo [3/4] Servislerin saglik durumlari kontrol ediliyor...
docker compose -f "%~dp0docker-compose.dev.yml" ps

echo.
echo [3.5/4] .NET projeleri derleniyor...
dotnet build "%~dp0SesliKitap.slnx" -c Debug
if %errorlevel% neq 0 (
    echo [HATA] Proje derlenemedi!
    if not "%~1"=="--no-pause" pause
    exit /b %errorlevel%
)

echo.
echo [4/4] Uygulama pencereleri aciliyor...

start "SesliKitap-API" cmd /k "cd /d "%~dp0src\SesliKitap.API" && dotnet run --no-build --launch-profile http"

docker inspect seslikitap-dev-worker > nul 2>&1
if %errorlevel% equ 0 (
    start "SesliKitap-Worker" cmd /k "docker logs -f seslikitap-dev-worker"
)

if exist "%~dp0src\SesliKitap.Frontend\package.json" (
    if not exist "%~dp0src\SesliKitap.Frontend\node_modules" (
        echo [UI] Frontend bagimliliklari yukleniyor ^(npm ci^)...
        pushd "%~dp0src\SesliKitap.Frontend"
        call npm ci
        popd
    )
    start "SesliKitap-UI" cmd /k "cd /d "%~dp0src\SesliKitap.Frontend" && npm start"
)

if not "%~1"=="--no-pause" (
    echo.
    echo Tarayici aciliyor...
    ping 127.0.0.1 -n 6 > nul
    if exist "%~dp0src\SesliKitap.Frontend\package.json" (
        start http://localhost:4300
    )
    start http://localhost:5201/swagger
)

echo.
echo ========================================================
echo SesliKitap Sistemi Basariyla Baslatildi!
echo Arayuz:    http://localhost:4300
echo Swagger:   http://localhost:5201/swagger
echo MinIO:     http://localhost:9011 (minioadmin / minioadmin)
echo RabbitMQ:  http://localhost:15674 (guest / guest)
echo Not:       API erisim anahtari .env dosyasindaki ADMIN_API_KEY degeridir.
echo ========================================================
if not "%~1"=="--no-pause" (
    echo Bu pencereyi kapatabilirsiniz.
    pause
)
