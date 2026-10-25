<#
.SYNOPSIS
    SesliKitap PostgreSQL veritabanı ve MinIO nesne deposu geri yükleme betiği (Faz 9).
.DESCRIPTION
    Belirtilen yedek klasöründeki db.dump dosyasını PostgreSQL'e (pg_restore --clean --if-exists)
    ve minio/ altındaki dosyaları MinIO bucket'ına geri yükler.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFolder,
    [string]$PostgresContainer = "seslikitap-dev-postgres",
    [string]$MinioContainer = "seslikitap-dev-minio",
    [string]$DbName = "seslikitap",
    [string]$DbUser = "postgres",
    [string]$Bucket = "seslikitap",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupFolder)) {
    throw "Yedek klasörü bulunamadı: $BackupFolder"
}

$dumpPath = Join-Path $BackupFolder "db.dump"
$minioBackupDir = Join-Path $BackupFolder "minio"

if (-not (Test-Path $dumpPath)) {
    throw "Veritabanı yedek dosyası bulunamadı: $dumpPath"
}

if (-not $Force) {
    $confirm = Read-Host "DIKKAT: Mevcut '$DbName' veritabanı ve MinIO '$Bucket' verileri üzerine yazılacak! Devam edilsin mi? (EVET/hayir)"
    if ($confirm -ne "EVET") {
        Write-Host "Geri yükleme iptal edildi." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host "[1/2] PostgreSQL veritabanı geri yükleniyor ($PostgresContainer)..." -ForegroundColor Cyan
docker cp $dumpPath "${PostgresContainer}:/tmp/restore.dump"
docker exec $PostgresContainer pg_restore --clean --if-exists --no-owner -U $DbUser -d $DbName /tmp/restore.dump
$pgExit = $LASTEXITCODE
docker exec $PostgresContainer rm -f /tmp/restore.dump
if ($pgExit -ne 0) {
    throw "pg_restore işlemi başarısız oldu (Çıkış kodu: $pgExit)."
}

Write-Host "[2/2] MinIO nesne deposu geri yükleniyor ($MinioContainer)..." -ForegroundColor Cyan
if (Test-Path $minioBackupDir) {
    docker exec $MinioContainer mkdir -p "/data/$Bucket"
    docker cp "$minioBackupDir/." "${MinioContainer}:/data/$Bucket/"
}

Write-Host "Geri yükleme başarıyla tamamlandı." -ForegroundColor Green
