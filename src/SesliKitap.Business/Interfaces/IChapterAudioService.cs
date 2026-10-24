namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Bölüm ses dosyasının HTTP Range akışı için önbelleklenen meta bilgileri.
/// </summary>
/// <param name="ObjectKey">MinIO nesne anahtarı.</param>
/// <param name="Length">Dosya bayt boyutu.</param>
/// <param name="ETag">HTTP ETag değeri (tırnaklı).</param>
/// <param name="ContentType">MIME tipi (<c>audio/ogg</c> veya <c>audio/mpeg</c>).</param>
public sealed record ChapterAudioInfo(
    string ObjectKey,
    long Length,
    string ETag,
    string ContentType);

/// <summary>
/// Bölüm ses akışı (HTTP Range) ve cümle zaman damgalarını sunan servis sözleşmesi (Bölüm 4.15.4).
/// </summary>
public interface IChapterAudioService
{
    /// <summary>
    /// Bölümün istenen formattaki (<c>opus</c> veya <c>mp3</c>) ses dosyası meta bilgisini getirir.
    /// </summary>
    Task<ChapterAudioInfo> GetAudioInfoAsync(Guid chapterId, string format = "opus", CancellationToken ct = default);

    /// <summary>
    /// MinIO üzerindeki ses dosyasının belirtilen bayt aralığını hedef akışa kopyalar.
    /// </summary>
    Task CopyRangeToAsync(
        string objectKey,
        long offset,
        long length,
        Stream destination,
        CancellationToken ct = default);

    /// <summary>
    /// Bölümün cümle zaman damgaları (<c>timestamps.json</c>) ham JSON metnini getirir (Redis önbellekli).
    /// </summary>
    Task<string> GetTimestampsJsonAsync(Guid chapterId, CancellationToken ct = default);
}
