<#
.SYNOPSIS
    PostgreSQL veritabanı ile MinIO nesne deposu arasındaki tutarlılığı doğrular (Faz 9 - 5.10.8).
.DESCRIPTION
    1. Ses durumu 'Hazir' (3) olan tüm bölümlerin opus_yolu, mp3_yolu ve zaman_damgasi_yolu
       nesnelerinin MinIO üzerinde var olduğunu doğrular.
    2. MinIO'daki books/{bookId} klasörlerinin veritabanında karşılığı olup olmadığını kontrol eder.
#>
param(
    [string]$PostgresContainer = "seslikitap-dev-postgres",
    [string]$MinioContainer = "seslikitap-dev-minio",
    [string]$DbName = "seslikitap",
    [string]$DbUser = "postgres",
    [string]$Bucket = "seslikitap",
    [switch]$CleanupOrphans
)

$ErrorActionPreference = "Stop"
$missingCount = 0
$orphanCount = 0

Write-Host "[1/2] Hazır bölümlerin MinIO ses ve zaman damgası dosyaları kontrol ediliyor..." -ForegroundColor Cyan
$sql = "SELECT id, opus_yolu, mp3_yolu, zaman_damgasi_yolu FROM book_chapters WHERE ses_durumu = 3;"
$rows = docker exec $PostgresContainer psql -U $DbUser -d $DbName -t -A -F "|" -c $sql

foreach ($line in $rows) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line.Split("|")
    if ($parts.Length -lt 4) { continue }
    $chId = $parts[0]
    foreach ($key in @($parts[1], $parts[2], $parts[3])) {
        if ([string]::IsNullOrWhiteSpace($key)) {
            Write-Host "  [EKSİK YOL] Bölüm $chId için nesne anahtarı boş!" -ForegroundColor Red
            $missingCount++
            continue
        }
        docker exec $MinioContainer test -e "/data/$Bucket/$key"
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  [EKSİK NESNE] Bölüm $chId -> /data/$Bucket/$key bulunamadı!" -ForegroundColor Red
            $missingCount++
        }
    }
}

Write-Host "[2/2] MinIO üzerindeki sahipsiz kitap klasörleri kontrol ediliyor..." -ForegroundColor Cyan
$bookIdsRaw = docker exec $PostgresContainer psql -U $DbUser -d $DbName -t -A -c "SELECT id FROM books;"
$knownBooks = @{}
foreach ($bId in $bookIdsRaw) {
    if (-not [string]::IsNullOrWhiteSpace($bId)) {
        $knownBooks[$bId.Trim().ToLowerInvariant()] = $true
    }
}

$minioBookDirs = docker exec $MinioContainer sh -c "ls -1 /data/$Bucket/books 2>/dev/null || true"
foreach ($dir in $minioBookDirs) {
    $cleanDir = "$dir".Trim().ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($cleanDir)) { continue }
    if (-not $knownBooks.ContainsKey($cleanDir)) {
        if ($CleanupOrphans) {
            docker exec $MinioContainer rm -rf "/data/$Bucket/books/$cleanDir"
            Write-Host "  [TEMİZLENDİ] Sahipsiz MinIO klasörü silindi: books/$cleanDir" -ForegroundColor Cyan
        } else {
            Write-Host "  [SAHİPSİZ KLASÖR] MinIO books/$cleanDir veritabanında bulunamadı!" -ForegroundColor Yellow
            $orphanCount++
        }
    }
}

if ($missingCount -eq 0 -and $orphanCount -eq 0) {
    Write-Host "Bütünlük doğrulaması BAŞARILI: Eksik nesne (0), sahipsiz klasör (0)." -ForegroundColor Green
    exit 0
} else {
    Write-Host "Bütünlük doğrulaması tamamlandı: Eksik nesne=$missingCount, Sahipsiz klasör=$orphanCount" -ForegroundColor Red
    exit 1
}
