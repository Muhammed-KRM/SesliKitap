param(
    [string]$ApiUrl = "http://localhost:5201",
    [string]$ApiKey = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repoRoot ".env"

if ([string]::IsNullOrWhiteSpace($ApiKey) -and (Test-Path $envPath)) {
    $line = Get-Content $envPath | Where-Object { $_ -match "^ADMIN_API_KEY=" } | Select-Object -First 1
    if ($line) {
        $ApiKey = ($line -split "=", 2)[1].Trim()
    }
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Error "ADMIN_API_KEY bulunamadı! .env dosyasını oluşturun veya -ApiKey parametresi verin."
}

$hazirSeslerDir = Join-Path $repoRoot "referans-ses\hazir_sesler"
if (-not (Test-Path $hazirSeslerDir)) {
    Write-Error "Hazır sesler dizini bulunamadı: $hazirSeslerDir"
}

Write-Host "[Seed] Hazır Türkçe Ses Profilleri API'ye yükleniyor ($ApiUrl)..." -ForegroundColor Cyan

$presets = Get-ChildItem -Path $hazirSeslerDir -Directory
foreach ($folder in $presets) {
    $presetJsonPath = Join-Path $folder.FullName "preset.json"
    $wavPath = Join-Path $folder.FullName "reference.wav"
    $txtPath = Join-Path $folder.FullName "reference.txt"

    if (-not (Test-Path $presetJsonPath) -or -not (Test-Path $wavPath) -or -not (Test-Path $txtPath)) {
        Write-Warning "Eksik dosya: $($folder.Name)"
        continue
    }

    $preset = Get-Content $presetJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $refText = (Get-Content $txtPath -Raw -Encoding UTF8).Trim()
    $isDefault = if ($folder.Name -like "01_*") { "true" } else { "false" }

    Write-Host "  -> Yükleniyor: $($preset.ad)..." -NoNewline
    $resp = curl.exe -s -w "`n%{http_code}" -X POST "$ApiUrl/api/voice-profiles" `
        -H "X-API-Key: $ApiKey" `
        -F "ad=$($preset.ad)" `
        -F "aciklama=$($preset.aciklama)" `
        -F "cinsiyet=$($preset.cinsiyet)" `
        -F "referansMetin=$refText" `
        -F "kullanimOnayi=true" `
        -F "varsayilanMi=$isDefault" `
        -F "sesDosyasi=@$wavPath;type=audio/wav"

    $lines = $resp -split "`n"
    $code = $lines[-1].Trim()
    if ($code -eq "202" -or $code -eq "201") {
        Write-Host " Başarılı (HTTP $code)" -ForegroundColor Green
    } elseif ($code -eq "409") {
        Write-Host " Zaten Mevcut (HTTP 409)" -ForegroundColor Yellow
    } else {
        Write-Host " Hata (HTTP $code): $($lines[0])" -ForegroundColor Red
    }
}

Write-Host "[Seed] Tüm ses profilleri hazır!" -ForegroundColor Cyan
