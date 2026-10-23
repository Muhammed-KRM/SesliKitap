namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Hata-açık (fail-open) Redis önbellek ve dağıtık kilit servisi sözleşmesi.
/// Redis erişilemez olduğunda sistem istisna fırlatmadan veritabanı üzerinden çalışmaya devam eder.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Belirtilen anahtardaki JSON değerini okuyup <typeparamref name="T"/> tipine çözer.
    /// </summary>
    /// <typeparam name="T">Hedef tip.</typeparam>
    /// <param name="key">Redis anahtarı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Önbellekte varsa nesne, yoksa veya Redis erişilemezse <c>default</c>.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Verilen nesneyi JSON olarak serileştirip belirtilen süreyle (TTL) Redis'e yazar.
    /// </summary>
    /// <typeparam name="T">Nesne tipi.</typeparam>
    /// <param name="key">Redis anahtarı.</param>
    /// <param name="value">Yazılacak nesne.</param>
    /// <param name="ttl">Geçerlilik süresi.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Önbellekte varsa değeri döner; yoksa <paramref name="factory"/> ile üretip önbelleğe yazar ve döner (Cache-Aside).
    /// </summary>
    /// <typeparam name="T">Değer tipi.</typeparam>
    /// <param name="key">Redis anahtarı.</param>
    /// <param name="factory">Veri üretici fonksiyon.</param>
    /// <param name="ttl">Geçerlilik süresi.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Önbellekten veya üreticiden gelen değer.</returns>
    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen anahtarı önbellekten siler.
    /// </summary>
    /// <param name="key">Silinecek Redis anahtarı.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Verilen önekle başlayan tüm anahtarları <c>SCAN</c> komutuyla bulup siler (<c>KEYS *</c> kullanılmaz).
    /// </summary>
    /// <param name="prefix">Anahtar öneki (örn. <c>sk:books:list:</c>).</param>
    /// <param name="ct">İptal belirteci.</param>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>
    /// Atomik <c>SET key value NX EX</c> komutuyla dağıtık kilit almaya çalışır.
    /// </summary>
    /// <param name="key">Kilit anahtarı.</param>
    /// <param name="value">Kilit sahibi değeri (örn. <c>jobId</c>).</param>
    /// <param name="ttl">Kilit zaman aşımı süresi.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Kilit alındıysa <c>true</c>, zaten kilitliyse <c>false</c>.</returns>
    Task<bool> TryAcquireLockAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Ham metin (JSON çözmeden) değerini okur.
    /// </summary>
    /// <param name="key">Redis anahtarı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Metin değeri veya <c>null</c>.</returns>
    Task<string?> GetStringAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Ham metin değerini belirtilen süreyle Redis'e yazar.
    /// </summary>
    /// <param name="key">Redis anahtarı.</param>
    /// <param name="value">Metin değeri.</param>
    /// <param name="ttl">Geçerlilik süresi.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task SetStringAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default);
}
