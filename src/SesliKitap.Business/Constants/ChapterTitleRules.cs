using SesliKitap.Business.Helpers;

namespace SesliKitap.Business.Constants;

/// <summary>
/// Otomatik olarak seslendirme dışı bırakılacak bölüm başlıklarını değerlendiren kural sınıfı.
/// Python Worker'daki <c>excluded_chapter_titles.yaml</c> ile aynı kuralları uygular.
/// </summary>
public static class ChapterTitleRules
{
    private static readonly (string NormalizedPattern, string Reason)[] Rules =
    [
        ("icindekiler", "İçindekiler başlığı"),
        ("table of contents", "İçindekiler başlığı"),
        ("kaynakca", "Kaynakça bölümü"),
        ("kaynaklar", "Kaynaklar bölümü"),
        ("bibliyografya", "Bibliyografya bölümü"),
        ("dizin", "Dizin bölümü"),
        ("indeks", "İndeks bölümü"),
        ("index", "İndeks bölümü"),
        ("kunye", "Künye bölümü"),
        ("telif hakki", "Telif hakkı sayfası"),
        ("copyright", "Telif hakkı sayfası"),
        ("isbn", "Künye / ISBN sayfası"),
        ("dipnotlar", "Dipnotlar bölümü"),
        ("sonnotlar", "Sonnotlar bölümü")
    ];

    /// <summary>
    /// Verilen bölüm başlığının otomatik hariç tutulup tutulmayacağını değerlendirir.
    /// </summary>
    /// <param name="title">Bölüm başlığı.</param>
    /// <returns>Hariç tutulmalıysa <c>(false, neden)</c>, dahil edilmeliyse <c>(true, null)</c>.</returns>
    public static (bool Included, string? ExclusionReason) Evaluate(string? title)
    {
        var normalized = TurkishTextHelper.NormalizeForSearch(title);
        if (string.IsNullOrEmpty(normalized))
        {
            return (true, null);
        }

        foreach (var (pattern, reason) in Rules)
        {
            if (normalized == pattern || normalized.StartsWith(pattern + " ", StringComparison.Ordinal))
            {
                return (false, reason);
            }
        }

        return (true, null);
    }
}
