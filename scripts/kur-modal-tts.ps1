# SesliKitap - Modal.com GPU TTS (VoxCPM2) tek komutluk kurulum betigi.
# Kullanim (repo kokunden):   powershell -ExecutionPolicy Bypass -File scripts\kur-modal-tts.ps1
#
# Yaptiklari:
#   1) gpu\.venv icine Modal CLI kurar (yoksa)
#   2) Modal hesabinda oturum yoksa tarayicida giris ister (TEK MANUEL ADIM)
#   3) .env icindeki TTS_API_TOKEN ile Modal secret'ini olusturur
#   4) Iki model dagitir: temel VoxCPM2 ve Turkce LoRA'li Trendyol-TTS (A/B kulak testi icin)
#   5) .env'e URL'leri yazar (varsayilan: temel model), worker'i yeniden baslatir, saglik kontrolu yapar
param(
    [ValidateSet("both", "base", "trendyol")] [string]$Model = "both",
    [ValidateSet("base", "trendyol")] [string]$Active = "base"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$envFile = Join-Path $root ".env"
$venv = Join-Path $root "gpu\.venv"
$modal = Join-Path $venv "Scripts\modal.exe"

function Get-EnvValue([string]$name) {
    $line = Get-Content $envFile | Where-Object { $_ -match "^$name=" } | Select-Object -First 1
    if (-not $line) { return $null }
    return ($line -replace "^$name=", "").Trim()
}

function Set-EnvValue([string]$name, [string]$value) {
    $lines = Get-Content $envFile
    if ($lines | Where-Object { $_ -match "^$name=" }) {
        $lines = $lines | ForEach-Object { if ($_ -match "^$name=") { "$name=$value" } else { $_ } }
    } else {
        $lines = @($lines) + "$name=$value"
    }
    Set-Content -Path $envFile -Value $lines -Encoding UTF8
}

# 1) Modal CLI
if (-not (Test-Path $modal)) {
    Write-Host "[1/6] Modal CLI kuruluyor (gpu\.venv)..." -ForegroundColor Cyan
    python -m venv $venv
    & (Join-Path $venv "Scripts\python.exe") -m pip install --quiet --upgrade pip modal
} else {
    Write-Host "[1/6] Modal CLI hazir." -ForegroundColor Green
}

# 2) Oturum
$tomlPath = Join-Path $env:USERPROFILE ".modal.toml"
if (-not (Test-Path $tomlPath)) {
    Write-Host "[2/6] Modal oturumu yok. Acilan tarayici sekmesinde giris yapip 'Authorize' deyin..." -ForegroundColor Yellow
    & $modal setup
    if (-not (Test-Path $tomlPath)) { throw "Modal girisi tamamlanmadi. Betigi tekrar calistirin." }
} else {
    Write-Host "[2/6] Modal oturumu mevcut." -ForegroundColor Green
}

# 3) Secret
$token = Get-EnvValue "TTS_API_TOKEN"
if (-not $token -or $token -match "degistir_beni") { throw ".env icinde gecerli bir TTS_API_TOKEN yok." }
Write-Host "[3/6] Modal secret olusturuluyor (seslikitap-tts-secret)..." -ForegroundColor Cyan
& $modal secret create seslikitap-tts-secret "TTS_API_TOKEN=$token" "TTS_AUTH_TOKEN=$token" --force | Out-Null

# 4) Dagitim
function Deploy-Model([string]$modelId, [string]$suffix) {
    Push-Location (Join-Path $root "gpu")
    try {
        $env:SK_TTS_MODEL_ID = $modelId
        $env:SK_TTS_APP_SUFFIX = $suffix
        Write-Host "      Dagitiliyor: $modelId (ilk seferde imaj olusturma birkac dakika surer)..." -ForegroundColor Cyan
        $out = & $modal deploy modal_deploy.py 2>&1 | Tee-Object -Variable log | ForEach-Object { "$_" }
        $match = ($out | Select-String -Pattern "https://[^\s]+\.modal\.run" -AllMatches).Matches | Select-Object -Last 1
        if (-not $match) { throw "Dagitim ciktisinda URL bulunamadi:`n$($out -join "`n")" }
        return $match.Value.TrimEnd("/")
    } finally {
        Remove-Item Env:\SK_TTS_MODEL_ID -ErrorAction SilentlyContinue
        Remove-Item Env:\SK_TTS_APP_SUFFIX -ErrorAction SilentlyContinue
        Pop-Location
    }
}

Write-Host "[4/6] GPU sunuculari dagitiliyor..." -ForegroundColor Cyan
$urls = @{}
if ($Model -in @("both", "base"))     { $urls["base"]     = Deploy-Model "openbmb/VoxCPM2" "" }
if ($Model -in @("both", "trendyol")) { $urls["trendyol"] = Deploy-Model "Trendyol/Trendyol-TTS" "trendyol" }

# 5) .env + worker
if ($urls.ContainsKey("base"))     { Set-EnvValue "TTS_MODAL_URL_BASE" $urls["base"] }
if ($urls.ContainsKey("trendyol")) { Set-EnvValue "TTS_MODAL_URL_TRENDYOL" $urls["trendyol"] }
if (-not $urls.ContainsKey($Active)) { $Active = @($urls.Keys)[0] }
Set-EnvValue "TTS_MODAL_URL" $urls[$Active]
Set-EnvValue "TTS_MODAL_HEALTH_URL" ($urls[$Active] + "/v1/health")
Write-Host "[5/6] .env guncellendi. Aktif motor: $Active -> $($urls[$Active])" -ForegroundColor Green

docker compose -f (Join-Path $root "docker-compose.dev.yml") up -d --force-recreate --no-deps python-worker | Out-Null

# 6) Saglik kontrolu (soguk baslatma + model indirme 5-10 dk surebilir)
Write-Host "[6/6] Saglik kontrolu (model ilk seferde indirilir, 10 dakikaya kadar beklenir)..." -ForegroundColor Cyan
$headers = @{ Authorization = "Bearer $token" }
$ok = $false
foreach ($name in $urls.Keys) {
    for ($i = 0; $i -lt 40; $i++) {
        try {
            $h = Invoke-RestMethod -Uri ($urls[$name] + "/v1/health") -Headers $headers -TimeoutSec 60
            Write-Host "      $name : $($h.status) | model yuklu=$($h.modelLoaded) | gpu=$($h.gpu)" -ForegroundColor Green
            $ok = $true; break
        } catch { Start-Sleep -Seconds 15 }
    }
}
if (-not $ok) { Write-Host "UYARI: Saglik kontrolu basarisiz. 'modal app logs' ile bakin." -ForegroundColor Red; exit 1 }
Write-Host "`nHAZIR. Siradaki adim: referans sesinizi 'referans-ses' klasorune koyup" -ForegroundColor Green
Write-Host "  python scripts\ses-karsilastirma.py   komutuyla modelleri dinleyip karsilastirin." -ForegroundColor Green
