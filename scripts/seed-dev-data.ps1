param(
    [string]$ApiUrl = "http://localhost:5201",
    [string]$ApiKey = ""
)

$ErrorActionPreference = "Stop"

# .env dosyasından ADMIN_API_KEY oku (eğer parametre verilmediyse)
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

Write-Host "[Seed] SesliKitap geliştirme verisi yükleniyor ($ApiUrl)..." -ForegroundColor Cyan

# 1. Örnek telifsiz Türkçe TXT kitap oluştur ve yükle
$tempTxt = Join-Path $env:TEMP "ornek-masallar-seslikitap.txt"
$sampleBookText = @"
BİRİNCİ BÖLÜM
Ormanın Kalbindeki Kütüphane

Evvel zaman içinde, kalbur saman içinde, ulu çınarların gölgesinde saklı küçük bir taş kütüphane vardı. Bu kütüphanenin raflarında unutulmuş masallar, eski seyahatnameler ve el yazması şiirler diziliydi. Her sabah güneş doğarken pencerelerden süzülen kehribar rengi ışık, tozlu ciltlerin üzerindeki altın yaldızlı harfleri aydınlatırdı.

Kütüphanenin bekçisi Bilge İhtiyar, her akşam fenerini yakar ve seçtiği bir kitabı yüksek sesle okurdu. Rüzgar yaprakları hışırdatırken, ormanın sakinleri pencerenin pervazına toplanır, anlatılan hikayeleri can kulağıyla dinlerdi.

İKİNCİ BÖLÜM
Kayıp Pusulanın Sırrı

Bir sonbahar sabahı kütüphanenin kapısına genç bir gezgin geldi. Elinde pirinçten yapılmış eski bir pusula tutuyordu ama pusulanın ibresi kuzeyi değil, kütüphanenin en üst rafındaki mavi ciltli atlası gösteriyordu.

Bilge İhtiyar gülümseyerek merdiveni dayadı ve mavi atlası indirdi. Sayfaları çevirdiklerinde, haritanın kenarına düşülmüş Türkçe notlar ve yıldız işaretleri belirdi. Gezginin aradığı yol, aslında kelimelerin arasında gizliydi.
"@
[System.IO.File]::WriteAllText($tempTxt, $sampleBookText, [System.Text.Encoding]::UTF8)

try {
    Write-Host "[1/2] Örnek TXT kitap yükleniyor..."
    $bookResp = curl.exe -s -w "`n%{http_code}" -X POST "$ApiUrl/api/books/upload" `
        -H "X-API-Key: $ApiKey" `
        -F "file=@$tempTxt;type=text/plain" `
        -F "baslik=Ormanın Kalbindeki Kütüphane (Örnek Kitap)" `
        -F "yazar=Anonim Masal"
    $lines = $bookResp -split "`n"
    $code = $lines[-1].Trim()
    if ($code -eq "202" -or $code -eq "201") {
        Write-Host "  -> Örnek kitap başarıyla yüklendi (HTTP $code)." -ForegroundColor Green
    } elseif ($code -eq "409") {
        Write-Host "  -> Örnek kitap zaten kütüphanede mevcut (HTTP 409)." -ForegroundColor Yellow
    } else {
        Write-Warning "  -> Kitap yükleme yanıtı: HTTP $code - $($lines[0])"
    }
} finally {
    if (Test-Path $tempTxt) { Remove-Item $tempTxt -Force }
}

# 2. Örnek 10 saniyelik 24 kHz mono WAV ses dosyası üret ve ses profili yükle
$tempWav = Join-Path $env:TEMP "ornek-ses-profili.wav"
$sampleRate = 24000
$durationSec = 10
$numSamples = $sampleRate * $durationSec
$bytesPerSample = 2
$dataSize = $numSamples * $bytesPerSample

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
$bw.Write([System.Text.Encoding]::ASCII.GetBytes("RIFF"))
$bw.Write([int](36 + $dataSize))
$bw.Write([System.Text.Encoding]::ASCII.GetBytes("WAVEfmt "))
$bw.Write([int]16)
$bw.Write([int16]1) # PCM
$bw.Write([int16]1) # Mono
$bw.Write([int]$sampleRate)
$bw.Write([int]($sampleRate * $bytesPerSample))
$bw.Write([int16]$bytesPerSample)
$bw.Write([int16]16)
$bw.Write([System.Text.Encoding]::ASCII.GetBytes("data"))
$bw.Write([int]$dataSize)

for ($i = 0; $i -lt $numSamples; $i++) {
    $t = $i / [double]$sampleRate
    $val = [Math]::Sin(2.0 * [Math]::PI * 220.0 * $t) * 0.35
    $sampleInt = [int16]($val * 32767)
    $bw.Write($sampleInt)
}
$bw.Flush()
[System.IO.File]::WriteAllBytes($tempWav, $ms.ToArray())
$bw.Close()
$ms.Close()

try {
    Write-Host "[2/2] Örnek ses profili yükleniyor..."
    $voiceResp = curl.exe -s -w "`n%{http_code}" -X POST "$ApiUrl/api/voice-profiles" `
        -H "X-API-Key: $ApiKey" `
        -F "ad=Örnek Anlatıcı (Geliştirme)" `
        -F "aciklama=Seed betiği ile oluşturulan test ses profili" `
        -F "cinsiyet=Belirtilmemis" `
        -F "referansMetin=Güneşli bir sonbahar sabahı, İstanbul boğazının serin suları hafifçe dalgalanıyordu." `
        -F "kullanimOnayi=true" `
        -F "varsayilanMi=true" `
        -F "sesDosyasi=@$tempWav;type=audio/wav"
    $vLines = $voiceResp -split "`n"
    $vCode = $vLines[-1].Trim()
    if ($vCode -eq "202" -or $vCode -eq "201") {
        Write-Host "  -> Örnek ses profili başarıyla oluşturuldu (HTTP $vCode)." -ForegroundColor Green
    } elseif ($vCode -eq "409") {
        Write-Host "  -> Örnek ses profili zaten mevcut (HTTP 409)." -ForegroundColor Yellow
    } else {
        Write-Warning "  -> Ses profili yanıtı: HTTP $vCode - $($vLines[0])"
    }
} finally {
    if (Test-Path $tempWav) { Remove-Item $tempWav -Force }
}

Write-Host "[Seed] Tamamlandı!" -ForegroundColor Cyan
