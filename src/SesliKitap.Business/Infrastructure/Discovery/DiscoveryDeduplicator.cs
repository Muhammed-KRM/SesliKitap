using SesliKitap.Business.DTOs.Discovery;
using SesliKitap.Business.Helpers;

namespace SesliKitap.Business.Infrastructure.Discovery;

/// <summary>
/// Farklı arama sağlayıcılarından gelen aynı kitaba ait sonuçları normalize edilmiş <c>başlık + yazar</c> anahtarı
/// ve bulanık benzerlik (Jaro-Winkler / normalize Levenshtein ≥ 0.92) ile tekilleştirir.
/// </summary>
public static class DiscoveryDeduplicator
{
    private const double SimilarityThreshold = 0.92;

    /// <summary>
    /// Ham keşif sonuçlarını tekilleştirir ve kaynak rozetlerini/formatları birleştirir.
    /// </summary>
    public static IReadOnlyList<DiscoveryResultDto> Deduplicate(IEnumerable<DiscoveryResultDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var clusters = new List<(string NormKey, DiscoveryResultDto Merged)>();

        foreach (var item in items)
        {
            var key = BuildKey(item.Baslik, item.Yazar);
            var matchedIndex = -1;

            for (var i = 0; i < clusters.Count; i++)
            {
                if (clusters[i].NormKey == key ||
                    ComputeSimilarity(clusters[i].NormKey, key) >= SimilarityThreshold)
                {
                    matchedIndex = i;
                    break;
                }
            }

            if (matchedIndex < 0)
            {
                clusters.Add((key, item));
            }
            else
            {
                var existing = clusters[matchedIndex].Merged;
                clusters[matchedIndex] = (clusters[matchedIndex].NormKey, Merge(existing, item));
            }
        }

        return clusters.Select(c => c.Merged).ToList();
    }

    private static DiscoveryResultDto Merge(DiscoveryResultDto primary, DiscoveryResultDto secondary)
    {
        var formats = primary.MevcutFormatlar
            .Concat(secondary.MevcutFormatlar)
            .Distinct()
            .ToList();

        var files = primary.Dosyalar
            .Concat(secondary.Dosyalar)
            .GroupBy(f => f.IndirmeUrl, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var sources = primary.Kaynaklar
            .Concat(secondary.Kaynaklar)
            .GroupBy(s => $"{s.SaglayiciKodu}:{s.KaynakKimligi}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        return primary with
        {
            Yazar = primary.Yazar ?? secondary.Yazar,
            YayinYili = primary.YayinYili ?? secondary.YayinYili,
            KapakUrl = primary.KapakUrl ?? secondary.KapakUrl,
            IndirmeSayisi = Math.Max(primary.IndirmeSayisi, secondary.IndirmeSayisi),
            MevcutFormatlar = formats,
            Dosyalar = files,
            Kaynaklar = sources,
            DogrulanmamisWebKaynagi = primary.DogrulanmamisWebKaynagi && secondary.DogrulanmamisWebKaynagi
        };
    }

    private static string BuildKey(string title, string? author)
    {
        var normTitle = TurkishTextHelper.NormalizeForSearch(title);
        var firstAuthor = string.IsNullOrWhiteSpace(author)
            ? string.Empty
            : author.Split([',', ';', '/'], StringSplitOptions.RemoveEmptyEntries)[0];
        var normAuthor = TurkishTextHelper.NormalizeForSearch(firstAuthor);
        return $"{normTitle}|{normAuthor}";
    }

    /// <summary>
    /// İki normalize dizge arasındaki [0, 1] benzerlik oranını hesaplar.
    /// </summary>
    public static double ComputeSimilarity(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return 1.0;
        }

        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return 0.0;
        }

        var maxLen = Math.Max(a.Length, b.Length);
        var dist = LevenshteinDistance(a, b);
        return 1.0 - ((double)dist / maxLen);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;
        var d = new int[m + 1];

        for (var j = 0; j <= m; j++)
        {
            d[j] = j;
        }

        for (var i = 1; i <= n; i++)
        {
            var prevDiag = d[0];
            d[0] = i;

            for (var j = 1; j <= m; j++)
            {
                var temp = d[j];
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[j] = Math.Min(
                    Math.Min(d[j] + 1, d[j - 1] + 1),
                    prevDiag + cost);
                prevDiag = temp;
            }
        }

        return d[m];
    }
}
