namespace SesliKitap.Business.Interfaces;

/// <summary>
/// MinIO / S3 nesne deposundaki bir nesnenin meta bilgisi.
/// </summary>
/// <param name="Key">Nesne anahtarı.</param>
/// <param name="Size">Bayt cinsinden nesne boyutu.</param>
/// <param name="ETag">Nesne ETag değeri (tırnaklı).</param>
/// <param name="ContentType">MIME içerik tipi.</param>
public sealed record StorageObjectInfo(
    string Key,
    long Size,
    string ETag,
    string ContentType);

/// <summary>
/// MinIO / S3 uyumlu nesne depolama servisi sözleşmesi.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uygulama başlangıcında varsayılan bucket yoksa oluşturur.
    /// </summary>
    /// <param name="ct">İptal belirteci.</param>
    Task EnsureBucketAsync(CancellationToken ct = default);

    /// <summary>
    /// Varsayılan bucket'ın mevcut ve erişilebilir olup olmadığını kontrol eder (Health check için).
    /// </summary>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Erişilebilirse <c>true</c>, aksi halde <c>false</c>.</returns>
    Task<bool> BucketExistsAsync(CancellationToken ct = default);

    /// <summary>
    /// Verilen akışı belirtilen anahtarla nesne deposuna yükler.
    /// </summary>
    /// <param name="key">Nesne anahtarı.</param>
    /// <param name="content">Yüklenecek veri akışı.</param>
    /// <param name="size">Bayt cinsinden boyut.</param>
    /// <param name="contentType">MIME içerik tipi.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task UploadAsync(string key, Stream content, long size, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Verilen nesneyi UTF-8 JSON olarak serileştirip nesne deposuna yükler.
    /// </summary>
    /// <typeparam name="T">Nesne tipi.</typeparam>
    /// <param name="key">Nesne anahtarı.</param>
    /// <param name="value">Serileştirilecek nesne.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task UploadJsonAsync<T>(string key, T value, CancellationToken ct = default);

    /// <summary>
    /// Nesne deposundaki JSON dosyasını okuyup <typeparamref name="T"/> tipine çözer.
    /// </summary>
    /// <typeparam name="T">Hedef tip.</typeparam>
    /// <param name="key">Nesne anahtarı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Nesne veya bulunamazsa <c>default</c>.</returns>
    Task<T?> ReadJsonAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Nesne deposundaki metin dosyasını UTF-8 string olarak okur.
    /// </summary>
    /// <param name="key">Nesne anahtarı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Metin içeriği veya bulunamazsa <c>null</c>.</returns>
    Task<string?> ReadTextAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Nesnenin tamamını veya belirtilen bayt aralığını (<c>Range</c>) hedef akışa kopyalar.
    /// </summary>
    /// <param name="key">Nesne anahtarı.</param>
    /// <param name="destination">Hedef akış.</param>
    /// <param name="offset">Başlangıç bayt konumu.</param>
    /// <param name="length">Okunacak bayt uzunluğu (<c>null</c> ise sona kadar).</param>
    /// <param name="ct">İptal belirteci.</param>
    Task CopyToAsync(string key, Stream destination, long offset = 0, long? length = null, CancellationToken ct = default);

    /// <summary>
    /// Nesnenin boyut, ETag ve MIME bilgilerini getirir.
    /// </summary>
    /// <param name="key">Nesne anahtarı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Bulunursa <see cref="StorageObjectInfo"/>, bulunamazsa <c>null</c>.</returns>
    Task<StorageObjectInfo?> GetInfoAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen nesneyi siler.
    /// </summary>
    /// <param name="key">Silinecek nesne anahtarı.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Verilen önekle başlayan tüm nesneleri toplu olarak siler.
    /// </summary>
    /// <param name="prefix">Nesne anahtarı öneki (örn. <c>books/{id}/</c>).</param>
    /// <param name="ct">İptal belirteci.</param>
    Task DeleteByPrefixAsync(string prefix, CancellationToken ct = default);
}
