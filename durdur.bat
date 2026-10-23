@echo off
echo ========================================================
echo       SesliKitap Sistemi Durduruluyor...
echo ========================================================
echo.

echo [1/2] Acik uygulama pencereleri ve API sureci kapatiliyor...
taskkill /F /IM "SesliKitap.API.exe" /T > nul 2>&1
taskkill /FI "WINDOWTITLE eq SesliKitap-*" /F /T > nul 2>&1

if /I "%~1"=="--temizle" (
    echo.
    echo [UYARI] --temizle parametresi verildi.
    echo Bu islem veritabani ve MinIO verilerini kalici olarak silecek ^(docker compose down -v^)!
    set /p ONAY="Devam etmek istiyor musunuz? (E/H): "
    if /I "%ONAY%"=="E" (
        echo [2/2] Docker servisleri ve birimler ^(volumes^) siliniyor...
        docker compose -f "%~dp0docker-compose.dev.yml" down -v
    ) else (
        echo Islem iptal edildi, yalnızca servisler durduruluyor...
        docker compose -f "%~dp0docker-compose.dev.yml" stop
    )
) else (
    echo [2/2] Docker servisleri durduruluyor ^(veriler korunur^)...
    docker compose -f "%~dp0docker-compose.dev.yml" stop
)

echo.
echo SesliKitap servisleri durduruldu.
if not "%~1"=="--no-pause" pause
