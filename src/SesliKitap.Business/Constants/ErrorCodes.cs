namespace SesliKitap.Business.Constants;

/// <summary>
/// <see cref="SesliKitap.Data.Entities.FunctionLog"/> ve hata yanıtlarında kullanılan standart hata kodları.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Kitap yükleme hatası.</summary>
    public const string BookUploadError = "BOOK_UPLOAD_ERR";

    /// <summary>Yinelenen kitap dosyası (aynı SHA-256 hash).</summary>
    public const string BookDuplicate = "BOOK_DUPLICATE";

    /// <summary>Kitap işleme sonucu kaydedilirken oluşan hata.</summary>
    public const string BookProcessedHandlerError = "BOOK_PROCESSED_HANDLER_ERR";

    /// <summary>Bölüm güncellenirken sürüm çakışması (iyimser kilitleme).</summary>
    public const string ChapterUpdateConflict = "CHAPTER_UPDATE_CONFLICT";

    /// <summary>Ses akışı (Range streaming) sırasında oluşan hata.</summary>
    public const string AudioStreamError = "AUDIO_STREAM_ERR";

    /// <summary>Ses profili yükleme/oluşturma hatası.</summary>
    public const string VoiceUploadError = "VOICE_UPLOAD_ERR";

    /// <summary>Sentez işi başlatma hatası.</summary>
    public const string SynthesisStartError = "SYNTHESIS_START_ERR";

    /// <summary>Aylık GPU bütçe limiti aşımı.</summary>
    public const string SynthesisBudgetExceeded = "SYNTHESIS_BUDGET_EXCEEDED";

    /// <summary>RabbitMQ mesaj tüketicisi (consumer) işleme hatası.</summary>
    public const string ConsumerError = "CONSUMER_ERR";

    /// <summary>Kitap keşif sağlayıcısı erişim veya zaman aşımı uyarısı.</summary>
    public const string DiscoveryProviderError = "DISCOVERY_PROVIDER_ERR";

    /// <summary>Geçersiz veya süresi dolmuş medya erişim belirteci.</summary>
    public const string MediaTokenInvalid = "MEDIA_TOKEN_INVALID";

    /// <summary>Redis önbellek erişim uyarısı (fail-open).</summary>
    public const string CacheAccessWarning = "CACHE_ACCESS_WARN";

    /// <summary>MinIO nesne depolama erişim hatası.</summary>
    public const string StorageAccessError = "STORAGE_ACCESS_ERR";

    /// <summary>Ele alınmamış beklenmeyen API istisnası.</summary>
    public const string ApiUnhandledError = "API_UNHANDLED_ERR";
}
