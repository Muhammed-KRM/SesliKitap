using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SesliKitap.Business.Helpers;

/// <summary>
/// Türkçe dil kurallarına duyarlı büyük/küçük harf dönüşümü, kelime sayımı, arama normalizasyonu ve slug üretimi yardımcıları.
/// </summary>
public static partial class TurkishTextHelper
{
    /// <summary>
    /// Standart Türkçe kültür bilgisi (<c>tr-TR</c>).
    /// </summary>
    public static readonly CultureInfo TrCulture = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>
    /// Metni Türkçe kurallarına göre (<c>I</c> → <c>ı</c>, <c>İ</c> → <c>i</c>) küçük harfe çevirir.
    /// </summary>
    public static string ToLowerTr(string? text)
    {
        return string.IsNullOrEmpty(text) ? string.Empty : text.ToLower(TrCulture);
    }

    /// <summary>
    /// Metni Türkçe kurallarına göre (<c>i</c> → <c>İ</c>, <c>ı</c> → <c>I</c>) büyük harfe çevirir.
    /// </summary>
    public static string ToUpperTr(string? text)
    {
        return string.IsNullOrEmpty(text) ? string.Empty : text.ToUpper(TrCulture);
    }

    /// <summary>
    /// Metindeki Unicode kelime sayısını hesaplar.
    /// </summary>
    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return WordRegex().Count(text);
    }

    /// <summary>
    /// Türkçe karakterleri ASCII karşılıklarına katlar ve küçük harfe çevirir (arama ve başlık eşleştirme için).
    /// </summary>
    public static string NormalizeForSearch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lower = ToLowerTr(text.Trim());
        var sb = new StringBuilder(lower.Length);

        foreach (var ch in lower)
        {
            var mapped = ch switch
            {
                'ç' => 'c',
                'ğ' => 'g',
                'ı' => 'i',
                'ö' => 'o',
                'ş' => 's',
                'ü' => 'u',
                'â' => 'a',
                'î' => 'i',
                'û' => 'u',
                _ => ch
            };
            sb.Append(mapped);
        }

        return MultiSpaceRegex().Replace(sb.ToString(), " ").Trim();
    }

    /// <summary>
    /// Metni dosya adı ve URL dostu ASCII slug biçimine dönüştürür (örn. <c>"Kürk Mantolu Madonna"</c> → <c>"kurk-mantolu-madonna"</c>).
    /// </summary>
    public static string Slugify(string? text)
    {
        var normalized = NormalizeForSearch(text);
        if (string.IsNullOrEmpty(normalized))
        {
            return "kitap";
        }

        var slug = NonAlphaNumericRegex().Replace(normalized, "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "kitap" : slug;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’][\p{L}\p{N}]+)*")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphaNumericRegex();
}
