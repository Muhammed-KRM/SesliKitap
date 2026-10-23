using System.Text.Json;
using System.Text.RegularExpressions;

namespace SesliKitap.Business.Helpers;

/// <summary>
/// Loglara hassas veri (şifre, API anahtarı, medya erişim belirteci, e-posta) sızmasını önleyen maskeleme yardımcısı.
/// </summary>
public static partial class SensitiveDataMasker
{
    [GeneratedRegex(@"[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"""(?i)(password|token|apiKey|api_key|secret|authorization|access_token|mediaToken|ttsApiToken|t)""\s*:\s*""([^""]+)""")]
    private static partial Regex JsonSensitiveKeyRegex();

    [GeneratedRegex(@"(?i)(X-API-Key|Bearer)\s*[:=]\s*([^\s,;&""']+)")]
    private static partial Regex HeaderApiKeyRegex();

    [GeneratedRegex(@"(?i)(?<=[?&])(t|access_token|apiKey|api_key|token)=([^&\s]+)")]
    private static partial Regex QuerySensitiveParamRegex();

    /// <summary>
    /// Metin içerisindeki JSON anahtarları, HTTP başlıkları, sorgu parametreleri ve e-posta adreslerindeki hassas verileri maskeler.
    /// </summary>
    /// <param name="input">Maskelenecek girdi metni.</param>
    /// <returns>Hassas değerleri <c>***</c> ile değiştirilmiş metin.</returns>
    public static string? Mask(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var masked = JsonSensitiveKeyRegex().Replace(input, "\"$1\": \"***\"");
        masked = HeaderApiKeyRegex().Replace(masked, "$1: ***");
        masked = QuerySensitiveParamRegex().Replace(masked, "$1=***");
        masked = EmailRegex().Replace(masked, "***@***.***");

        return masked;
    }

    /// <summary>
    /// JSON içeriğindeki hassas alanları maskeler.
    /// </summary>
    /// <param name="input">Maskelenecek JSON metni.</param>
    /// <returns>Maskelenmiş JSON metni.</returns>
    public static string? MaskJson(string? input) => Mask(input);

    /// <summary>
    /// URL sorgu dizesindeki (<c>?t=...&amp;access_token=...</c>) hassas parametre değerlerini maskeler.
    /// </summary>
    /// <param name="queryString">Ham sorgu dizesi.</param>
    /// <returns>Hassas parametreleri <c>***</c> ile maskelenmiş sorgu dizesi.</returns>
    public static string? MaskQueryString(string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return queryString;
        }

        var normalized = queryString.StartsWith('?') ? queryString : "?" + queryString;
        var masked = QuerySensitiveParamRegex().Replace(normalized, "$1=***");
        return queryString.StartsWith('?') ? masked : masked[1..];
    }

    /// <summary>
    /// PostgreSQL <c>jsonb</c> kolonlarına (<c>girdi_metadata</c>, <c>cikti_metadata</c>) yazılacak metnin
    /// her zaman geçerli bir JSON belgesi olmasını ve hassas verilerin maskelenmesini garanti eder.
    /// </summary>
    /// <param name="input">Ham JSON veya düz metin.</param>
    /// <returns>Geçerli ve maskelenmiş JSON dizesi veya <c>null</c>.</returns>
    public static string? EnsureValidJsonb(string? input)
    {
        var masked = Mask(input);
        if (string.IsNullOrWhiteSpace(masked))
        {
            return null;
        }

        var trimmed = masked.Trim();
        if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
            (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
        {
            try
            {
                using var _ = JsonDocument.Parse(trimmed);
                return trimmed;
            }
            catch (JsonException)
            {
                // Geçersiz JSON ise aşağıda güvenli nesneye sarılır.
            }
        }

        return JsonSerializer.Serialize(new { message = masked });
    }
}
