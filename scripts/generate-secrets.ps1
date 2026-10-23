<#
.SYNOPSIS
    SesliKitap için kriptografik olarak güvenli rastgele gizli anahtarlar (ADMIN_API_KEY, MEDIA_TOKEN_SECRET, TTS_API_TOKEN) üretir.
.PARAMETER WriteEnv
    Belirtilirse, proje kökündeki .env dosyasını (.env yoksa .env.example'dan kopyalayarak) üretilen anahtarlarla günceller.
#>
[CmdletBinding()]
param(
    [switch]$WriteEnv
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function New-Base64UrlSecret {
    param([int]$ByteLength = 48)
    $bytes = New-Object byte[] $ByteLength
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $base64 = [Convert]::ToBase64String($bytes)
    return $base64.TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$adminApiKey = New-Base64UrlSecret -ByteLength 48
$mediaTokenSecret = New-Base64UrlSecret -ByteLength 48
$ttsApiToken = New-Base64UrlSecret -ByteLength 48

if ($WriteEnv) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $envExamplePath = Join-Path $repoRoot ".env.example"
    $envPath = Join-Path $repoRoot ".env"

    if (-not (Test-Path $envPath)) {
        if (-not (Test-Path $envExamplePath)) {
            throw ".env.example bulunamadı: $envExamplePath"
        }
        Copy-Item -Path $envExamplePath -Destination $envPath
        Write-Host ".env dosyası .env.example şablonundan oluşturuldu." -ForegroundColor Cyan
    }

    $content = Get-Content -Path $envPath -Raw -Encoding UTF8
    $content = $content -replace '(?m)^ADMIN_API_KEY=.*$', "ADMIN_API_KEY=$adminApiKey"
    $content = $content -replace '(?m)^MEDIA_TOKEN_SECRET=.*$', "MEDIA_TOKEN_SECRET=$mediaTokenSecret"
    $content = $content -replace '(?m)^TTS_API_TOKEN=.*$', "TTS_API_TOKEN=$ttsApiToken"

    Set-Content -Path $envPath -Value $content -Encoding UTF8 -NoNewline
    Write-Host ".env içindeki gizli anahtarlar güvenli rastgele değerlerle güncellendi." -ForegroundColor Green
}
else {
    Write-Host "# Aşağıdaki değerleri .env dosyanıza kopyalayabilirsiniz (veya -WriteEnv parametresiyle çalıştırın):" -ForegroundColor Yellow
    Write-Output "ADMIN_API_KEY=$adminApiKey"
    Write-Output "MEDIA_TOKEN_SECRET=$mediaTokenSecret"
    Write-Output "TTS_API_TOKEN=$ttsApiToken"
}
