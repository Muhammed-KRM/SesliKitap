using System.ComponentModel.DataAnnotations;

namespace SesliKitap.Business.Options;

/// <summary>
/// API anahtarı, medya erişim belirteci ve CORS güvenlik yapılandırma seçenekleri (<c>Security</c> bölümü).
/// </summary>
public sealed class SecurityOptions
{
    /// <summary>Yapılandırma bölüm adı.</summary>
    public const string SectionName = "Security";

    /// <summary>
    /// Yönetici/istemci API erişim anahtarı (<c>X-API-Key</c>). En az 32 karakter olmalıdır.
    /// </summary>
    [Required(ErrorMessage = "ADMIN_API_KEY (Security:ApiKey) tanımlanmalıdır.")]
    [MinLength(32, ErrorMessage = "ADMIN_API_KEY en az 32 karakter uzunluğunda olmalıdır.")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Medya akışı (<c>audio/cover/sample</c>) HMAC-SHA256 imzalama gizli anahtarı. En az 32 karakter olmalıdır.
    /// </summary>
    [Required(ErrorMessage = "MEDIA_TOKEN_SECRET (Security:MediaTokenSecret) tanımlanmalıdır.")]
    [MinLength(32, ErrorMessage = "MEDIA_TOKEN_SECRET en az 32 karakter uzunluğunda olmalıdır.")]
    public string MediaTokenSecret { get; set; } = string.Empty;

    /// <summary>
    /// Medya erişim belirtecinin geçerlilik süresi (saat). Varsayılan: 6 saat.
    /// </summary>
    [Range(1, 168)]
    public int MediaTokenLifetimeHours { get; set; } = 6;

    /// <summary>
    /// CORS izin verilen kaynak adresleri.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = ["http://localhost:4300"];
}
