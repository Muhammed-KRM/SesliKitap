# ADR-004: ZIP Dışa Aktarımda Asenkron Akış ve Bellek Yönetimi

- **Tarih:** 2026-10-24
- **Durum:** Kabul

## Bağlam
Bölüm 4.15.4 (`ExportManager.cs`) uyarınca, bir kitabın hazır bölümlerini (`MP3` veya `Opus`), kapak görselini (`cover.jpg`) ve çalma listesini (`playlist.m3u`) tek bir sıkıştırmasız (`CompressionLevel.NoCompression`) ZIP arşivi olarak `GET /api/books/{id}/export?format=mp3|opus` uç noktasından indirmek gerekmektedir.

ASP.NET Core Kestrel sunucusunda `HttpResponse.Body` üzerinde senkron yazma (`Stream.Write`) varsayılan olarak kapalıdır (`AllowSynchronousIO = false`) ve thread-pool açlığına (thread starvation) yol açmamak için açılmamalıdır. `System.IO.Compression.ZipArchive` ise merkezi dizin (central directory) başlıklarını ve girdi sonlandırmalarını (`Dispose`) senkron `Write` çağrılarıyla yazar. Öte yandan, 10 saatlik bir sesli kitabın tüm bölümlerini `MemoryStream` içinde biriktirmek ~250–300 MB RAM tüketimine neden olur.

## Karar
`ExportManager.WriteZipAsync(Guid bookId, string format, Stream output, CancellationToken ct)` metodunda:
1. Sistem geçici dizininde `FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose` bayraklarıyla kendi kendini silen (`DeleteOnClose`) bir `FileStream` açılır.
2. `ZipArchive` bu geçici `FileStream` üzerinde `ZipArchiveMode.Create` ve `CompressionLevel.NoCompression` ile oluşturulur.
3. Her hazır bölüm MinIO'dan `OpenReadAsync` ile okunarak `await sourceStream.CopyToAsync(entryStream, ct)` ile doğrudan disk akışına aktarılır (RAM'de yalnızca 80 KB'lık kopyalama tamponu tutulur).
4. `cover.jpg` ve `#EXTM3U` / `#EXTINF` formatındaki UTF-8 `playlist.m3u` eklendikten sonra `ZipArchive` kapatılarak ZIP merkezi dizini diske yazılır.
5. Geçici `FileStream` başa (`Position = 0`) sarılır ve `await tempFileStream.CopyToAsync(output, 65536, ct)` ile tamamen asenkron olarak `HttpResponse.Body` (veya hedef akış) üzerine akıtılır; `using` bloğu bittiğinde işletim sistemi geçici dosyayı otomatik siler.

## Sonuçlar
- **Artılar:**
  - `AllowSynchronousIO` açılmaz; Kestrel asenkron G/Ç disiplini %100 korunur.
  - 300 MB'lık bir kitap dışa aktarılırken API sürecinin RAM artışı < 1 MB kalır (`NFR-05` API ≤ 400 MB RAM sınırına tam uyum).
  - İstemciye bozuk/yarım ZIP gitmesi önlenir (MinIO okuma hatası olursa HTTP başlıkları yazılmadan önce `ExternalServiceException` yakalanır).
  - `FileOptions.DeleteOnClose` sayesinde süreç çökse bile işletim sistemi geçici dosyayı temizler.
- **Eksiler:**
  - İlk baytın istemciye ulaşması, bölümlerin yerel geçici dosyaya kopyalanması kadar (yerel MinIO'da 50 MB için ~150–300 ms) gecikir.

## Alternatifler
1. **`MemoryStream` içinde ZIP oluşturmak:** Küçük kitaplarda hızlıdır ancak 250+ MB kitaplarda LOH (Large Object Heap) parçalanmasına ve yüksek RAM tüketimine yol açar (Reddedildi).
2. **`AllowSynchronousIO = true` açıp doğrudan `Response.Body`'ye yazmak:** Kestrel thread havuzunu bloklar ve ASP.NET Core güvenlik/performans önerilerine aykırıdır (Reddedildi).
