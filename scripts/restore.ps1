<#
.SYNOPSIS
    SesliKitap PostgreSQL veritabanı ve MinIO nesne deposu geri yükleme betiği (Faz 9).
.DESCRIPTION
    Belirtilen yedek klasöründeki veya ZIP arşivindeki db.dump dosyasını PostgreSQL'e
    ve minio/ altındaki dosyaları MinIO bucket'ına geri yükler.
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$BackupPath,
    [string]$PostgresContainer = "seslikitap-dev-postgres",
    [string]$MinioContainer = "seslikitap-dev-minio",
    [string]$DbName = "seslikitap",
    [string]$DbUser = "postgres",
    [string]$Bucket = "seslikitap",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupPath)) {
    throw "Yedek dosyası veya klasörü bulunamadı: $BackupPath"
}

$isZip = (Get-Item $BackupPath).Extension -eq ".zip"
$tempUnzipDir = $null

if ($isZip) {
    Write-Host "ZIP arşivi tespit edildi, geçici klasöre açılıyor..." -ForegroundColor Cyan
    $tempUnzipDir = Join-Path ([System.IO.Path]::GetTempPath()) ("sk_restore_" + (Get-Date -Format "yyyyMMdd_HHmmss"))
    New-Item -ItemType Directory -Path $tempUnzipDir -Force | Out-Null
    Expand-Archive -Path $BackupPath -DestinationPath $tempUnzipDir -Force
    $workingFolder = $tempUnzipDir
} else {
    $workingFolder = $BackupPath
}

try {
    $dumpPath = Join-Path $workingFolder "db.dump"
    $minioBackupDir = Join-Path $workingFolder "minio"

    if (-not (Test-Path $dumpPath)) {
        throw "Veritabanı yedek dosyası (db.dump) bulunamadı: $dumpPath"
    }

    if (-not $Force) {
        $confirm = Read-Host "DIKKAT: Mevcut '$DbName' veritabanı ve MinIO '$Bucket' verileri üzerine yazılacak! Devam edilsin mi? (EVET/hayir)"
        if ($confirm -ne "EVET") {
            Write-Host "Geri yükleme iptal edildi." -ForegroundColor Yellow
            exit 0
        }
    }

    Write-Host "[1/3] PostgreSQL bağlantıları kapatılıyor ve veritabanı geri yükleniyor..." -ForegroundColor Cyan
    # API veya arka plan servislerinin veritabanı bağlantılarını sonlandır ki tablo silme engellenmesin
    docker exec $PostgresContainer psql -U $DbUser -d postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DbName' AND pid <> pg_backend_pid();" 2>$null | Out-Null

    docker cp $dumpPath "${PostgresContainer}:/tmp/restore.dump"
    docker exec $PostgresContainer pg_restore --clean --if-exists --no-owner -U $DbUser -d $DbName /tmp/restore.dump
    $pgExit = $LASTEXITCODE
    docker exec $PostgresContainer rm -f /tmp/restore.dump
    if ($pgExit -ne 0 -and $pgExit -ne 1) {
        # pg_restore exit code 1 is usually non-fatal warnings (e.g. table does not exist on drop)
        throw "pg_restore işlemi başarısız oldu (Çıkış kodu: $pgExit)."
    }

    Write-Host "[2/3] MinIO nesne deposu dosyaları aktarılıyor ($MinioContainer)..." -ForegroundColor Cyan
    if (Test-Path $minioBackupDir) {
        docker exec $MinioContainer mkdir -p "/data/$Bucket"
        docker cp "$minioBackupDir/." "${MinioContainer}:/data/$Bucket/"
        docker exec $MinioContainer chmod -R 777 "/data/$Bucket" 2>$null | Out-Null
    }

    Write-Host "[3/3] SesliKitap Python Worker yeniden başlatılıyor..." -ForegroundColor Cyan
    docker restart seslikitap-dev-worker 2>$null | Out-Null

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host "Geri yükleme başarıyla tamamlandı!" -ForegroundColor Green
    Write-Host "Tüm kitaplar, üretilen sesler ve ilerleme başarıyla aktarıldı." -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Green
}
finally {
    if ($tempUnzipDir -and (Test-Path $tempUnzipDir)) {
        Remove-Item -Path $tempUnzipDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
