using System.ComponentModel.DataAnnotations;

namespace SesliKitap.Business.Options;

/// <summary>
/// Redis önbellek geçerlilik süresi (TTL) yapılandırma seçenekleri (<c>Cache</c> bölümü).
/// </summary>
public sealed class CacheOptions
{
    /// <summary>Yapılandırma bölüm adı.</summary>
    public const string SectionName = "Cache";

    /// <summary>Kütüphane kitap listesi önbellek süresi (saniye). Varsayılan: 60.</summary>
    [Range(1, 86400)]
    public int BookListSeconds { get; set; } = 60;

    /// <summary>Tekil kitap detayı önbellek süresi (dakika). Varsayılan: 5.</summary>
    [Range(1, 1440)]
    public int BookDetailMinutes { get; set; } = 5;

    /// <summary>Bölüm zaman damgaları ve ses meta bilgisi önbellek süresi (saat). Varsayılan: 24.</summary>
    [Range(1, 720)]
    public int ChapterMediaHours { get; set; } = 24;

    /// <summary>Ses profili listesi önbellek süresi (dakika). Varsayılan: 10.</summary>
    [Range(1, 1440)]
    public int VoiceListMinutes { get; set; } = 10;
}
