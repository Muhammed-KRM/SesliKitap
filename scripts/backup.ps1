<#
.SYNOPSIS
    SesliKitap PostgreSQL veritabanı ve MinIO nesne deposu yedekleme betiği (Faz 9).
.DESCRIPTION
    1. PostgreSQL veritabanını özel sıkıştırılmış biçimde (pg_dump -Fc) dışa aktarır.
    2. MinIO "seslikitap" bucket içeriğini (tts-cache hariç) yerel yedek klasörüne kopyalar.
    3. İsteğe bağlı olarak 7-Zip AES-256 (BACKUP_PASSWORD ortam değişkeni varsa) veya ZIP arşivi oluşturur.
    4. 14 günden eski yedekleri otomatik temizler.
#>
param(
    [string]$PostgresContainer = "seslikitap-dev-postgres",
    [string]$MinioContainer = "seslikitap-dev-minio",
    [string]$DbName = "seslikitap",
    [string]$DbUser = "postgres",
    [string]$Bucket = "seslikitap",
    [string]$OutputDir = "$PSScriptRoot\..\backups",
    [int]$RetentionDays = 14,
    [switch]$IncludeCache
)

$ErrorActionPreference = "Stop"

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupRoot = Join-Path (Resolve-Path "$PSScriptRoot\..").Path "backups"
if (-not (Test-Path $backupRoot)) {
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
}

$targetDir = Join-Path $backupRoot $timestamp
$minioBackupDir = Join-Path $targetDir "minio"
New-Item -ItemType Directory -Path $minioBackupDir -Force | Out-Null

Write-Host "[1/4] PostgreSQL yedeği alınıyor ($PostgresContainer -> db.dump)..." -ForegroundColor Cyan
$dumpPath = Join-Path $targetDir "db.dump"
docker exec $PostgresContainer pg_dump -Fc -U $DbUser -d $DbName -f /tmp/sk_db.dump
if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL pg_dump işlemi başarısız oldu."
}
docker cp "${PostgresContainer}:/tmp/sk_db.dump" $dumpPath
docker exec $PostgresContainer rm -f /tmp/sk_db.dump
if (-not (Test-Path $dumpPath) -or (Get-Item $dumpPath).Length -eq 0) {
    throw "PostgreSQL db.dump dosyası kopyalanamadı."
}

Write-Host "[2/4] MinIO nesne deposu yedeği alınıyor ($MinioContainer)..." -ForegroundColor Cyan
$containerTmp = "/tmp/sk_backup_$timestamp"
docker exec $MinioContainer mkdir -p $containerTmp
if ($IncludeCache) {
    docker exec $MinioContainer sh -c "cp -r /data/$Bucket/* $containerTmp/ 2>/dev/null || true"
} else {
    docker exec $MinioContainer sh -c "for d in /data/$Bucket/*; do base=`$(basename `"`$d`"); if [ `"`$base`" != 'cache' -a `"`$base`" != 'tts-cache' ]; then cp -r `"`$d`" $containerTmp/ 2>/dev/null || true; fi; done"
}
docker cp "${MinioContainer}:${containerTmp}/." $minioBackupDir
docker exec $MinioContainer rm -rf $containerTmp

Write-Host "[3/4] Yedek arşivleniyor..." -ForegroundColor Cyan
$sevenZip = Get-Command "7z" -ErrorAction SilentlyContinue
if ($sevenZip -and $env:BACKUP_PASSWORD) {
    $archivePath = Join-Path $backupRoot "seslikitap_backup_$timestamp.7z"
    & 7z a -t7z -mhe=on "-p$($env:BACKUP_PASSWORD)" $archivePath "$targetDir\*" | Out-Null
    Write-Host "Şifreli AES-256 arşiv oluşturuldu: $archivePath" -ForegroundColor Green
} else {
    $archivePath = Join-Path $backupRoot "seslikitap_backup_$timestamp.zip"
    Compress-Archive -Path "$targetDir\*" -DestinationPath $archivePath -Force
    Write-Host "ZIP arşivi oluşturuldu: $archivePath" -ForegroundColor Green
}

Write-Host "[4/4] $RetentionDays günden eski yedekler temizleniyor..." -ForegroundColor Cyan
$cutoff = (Get-Date).AddDays(-$RetentionDays)
Get-ChildItem -Path $backupRoot -Filter "seslikitap_backup_*.zip" | Where-Object { $_.LastWriteTime -lt $cutoff } | Remove-Item -Force
Get-ChildItem -Path $backupRoot -Filter "seslikitap_backup_*.7z" | Where-Object { $_.LastWriteTime -lt $cutoff } | Remove-Item -Force

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "Yedekleme başarıyla tamamlandı!" -ForegroundColor Green
Write-Host "Yedek Arşivi: $archivePath" -ForegroundColor Yellow
Write-Host "Yedek Klasörü: $targetDir" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Green
