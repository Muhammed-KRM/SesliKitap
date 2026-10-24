namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Kitabın hazır ses bölümlerini, kapağını ve M3U çalma listesini dışa aktaran servis sözleşmesi (Bölüm 4.15.4).
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Kitabın hazır ses bölümlerini (<c>mp3</c> veya <c>opus</c>), kapak görselini ve <c>playlist.m3u</c> dosyasını
    /// sıkıştırmasız (<c>NoCompression</c>) ZIP arşivi olarak hedef akışa yazar ve önerilen dosya adını döner.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <param name="format">Ses formatı (<c>mp3</c> veya <c>opus</c>).</param>
    /// <param name="output">Hedef asenkron çıktı akışı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>İndirme başlığı için güvenli Türkçe ZIP dosya adı.</returns>
    Task<string> WriteZipAsync(
        Guid bookId,
        string format,
        Stream output,
        CancellationToken ct = default);

    /// <summary>
    /// Kitabın güvenli ve okunabilir Türkçe ZIP dosya adını hesaplar.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <param name="format">Ses formatı (<c>mp3</c> veya <c>opus</c>).</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Güvenli dosya adı (ör. <c>Sefiller (MP3).zip</c>).</returns>
    Task<string> GetZipFileNameAsync(Guid bookId, string format, CancellationToken ct = default);

    /// <summary>
    /// Kitabın hazır M4B dışa aktarım dosyası MinIO'da mevcutsa bilgilerini döner; mevcut değilse Python Worker'a <c>BookExportRequestedEvent</c> yayınlar (Faz 8).
    /// </summary>
    Task<(bool IsReady, string StorageKey, string FileName, long Size)> GetOrRequestM4bAsync(
        Guid bookId,
        string traceId,
        CancellationToken ct = default);

    /// <summary>
    /// MinIO'daki hazır M4B dosyasını hedef akışa yazar (Faz 8).
    /// </summary>
    Task WriteM4bAsync(string storageKey, Stream output, CancellationToken ct = default);
}
