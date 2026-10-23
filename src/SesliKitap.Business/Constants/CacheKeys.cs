namespace SesliKitap.Business.Constants;

/// <summary>
/// Redis önbellek, dağıtık kilit ve iptal bayrağı anahtarlarını (<c>sk:</c> önekli) üreten sabitler.
/// Python worker <c>utils/constants.py</c> içindeki anahtar desenleriyle birebir uyumludur.
/// </summary>
public static class CacheKeys
{
    /// <summary>Tüm SesliKitap Redis anahtarlarının ortak öneki.</summary>
    public const string Prefix = "sk:";

    /// <summary>Kütüphane kitap listesi önbellek anahtarı öneki.</summary>
    public const string BookListPrefix = Prefix + "books:list:";

    /// <summary>Ses profili listesi önbellek anahtarı.</summary>
    public const string VoiceList = Prefix + "voices:list";

    /// <summary>
    /// Filtre özetine (hash) göre kütüphane kitap listesi önbellek anahtarı üretir.
    /// </summary>
    /// <param name="filterHash">Sorgu filtrelerinin SHA-1 özeti.</param>
    /// <returns>Redis anahtarı.</returns>
    public static string BookList(string filterHash) => $"{BookListPrefix}{filterHash}";

    /// <summary>
    /// Tekil kitap detayı önbellek anahtarı üretir.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <returns>Redis anahtarı.</returns>
    public static string Book(Guid bookId) => $"{Prefix}book:{bookId}";

    /// <summary>
    /// Bölüm cümle zaman damgaları (<c>timestamps.json</c>) önbellek anahtarı üretir.
    /// </summary>
    /// <param name="chapterId">Bölüm kimliği.</param>
    /// <returns>Redis anahtarı.</returns>
    public static string ChapterTimestamps(Guid chapterId) => $"{Prefix}chapter:{chapterId}:ts";

    /// <summary>
    /// Bölüm ses dosyası meta bilgisi (anahtar, boyut, ETag) önbellek anahtarı üretir.
    /// </summary>
    /// <param name="chapterId">Bölüm kimliği.</param>
    /// <param name="format">Ses formatı (<c>opus</c> veya <c>mp3</c>).</param>
    /// <returns>Redis anahtarı.</returns>
    public static string ChapterAudioInfo(Guid chapterId, string format) => $"{Prefix}chapter:{chapterId}:audio:{format}";

    /// <summary>
    /// Kitap keşif sağlayıcısı arama sonucu önbellek anahtarı üretir.
    /// </summary>
    /// <param name="provider">Sağlayıcı kodu (<c>ia</c>, <c>ol</c>, <c>gutenberg</c>, <c>web</c>).</param>
    /// <param name="queryHash">Sorgu ve sayfa bilgisinin SHA-1 özeti.</param>
    /// <returns>Redis anahtarı.</returns>
    public static string Discovery(string provider, string queryHash) => $"{Prefix}discovery:{provider}:{queryHash}";

    /// <summary>
    /// Kitap başına tek aktif sentez işi dağıtık kilit anahtarı üretir.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <returns>Redis anahtarı.</returns>
    public static string ActiveJob(Guid bookId) => $"{Prefix}book:{bookId}:active-job";

    /// <summary>
    /// Sentez işi iptal bayrağı anahtarı üretir (Worker her parçadan önce kontrol eder).
    /// </summary>
    /// <param name="jobId">Sentez işi kimliği.</param>
    /// <returns>Redis anahtarı.</returns>
    public static string JobCancel(Guid jobId) => $"{Prefix}job:{jobId}:cancel";
}
