using SesliKitap.Business.DTOs.Discovery;
using SesliKitap.Business.Helpers;
using SesliKitap.Data.Enums;

namespace SesliKitap.Business.Infrastructure.Discovery;

/// <summary>
/// Keşif arama sonuçlarını Türkçe dil eşleşmesi, dosya formatı (EPUB &gt; PDF), başlık/yazar uyumu,
/// indirme popülerliği ve kapak varlığına göre puanlayıp sıralar (Bölüm 4.15.4).
/// </summary>
public static class DiscoveryResultRanker
{
    /// <summary>
    /// Sonuçları sorguya göre puanlar ve azalan puan sırasıyla döndürür.
    /// </summary>
    public static IReadOnlyList<DiscoveryResultDto> RankAndSort(
        IEnumerable<DiscoveryResultDto> items,
        string query)
    {
        ArgumentNullException.ThrowIfNull(items);

        var normQuery = TurkishTextHelper.NormalizeForSearch(query);

        return items
            .Select(item =>
            {
                var score = ScoreSingle(item, normQuery);
                return item with { Puan = Math.Round(score, 2) };
            })
            .OrderByDescending(x => x.Puan)
            .ThenByDescending(x => x.IndirmeSayisi)
            .ToList();
    }

    private static double ScoreSingle(DiscoveryResultDto item, string normQuery)
    {
        var score = 0.0;

        // 1) Türkçe dil eşleşmesi (+3)
        if (item.Dil.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ||
            item.Dil.Equals("tur", StringComparison.OrdinalIgnoreCase))
        {
            score += 3.0;
        }

        // 2) İndirilebilir format (EPUB +2, PDF +1)
        if (item.MevcutFormatlar.Contains(DosyaFormati.Epub))
        {
            score += 2.0;
        }
        else if (item.MevcutFormatlar.Contains(DosyaFormati.Pdf))
        {
            score += 1.0;
        }

        // 3) Başlık eşleşmesi (+3 tam/içerme)
        var normTitle = TurkishTextHelper.NormalizeForSearch(item.Baslik);
        if (!string.IsNullOrEmpty(normQuery))
        {
            if (normTitle == normQuery)
            {
                score += 3.0;
            }
            else if (normTitle.Contains(normQuery, StringComparison.Ordinal))
            {
                score += 2.0;
            }
        }

        // 4) Yazar eşleşmesi (+2)
        if (!string.IsNullOrWhiteSpace(item.Yazar) && !string.IsNullOrEmpty(normQuery))
        {
            var normAuthor = TurkishTextHelper.NormalizeForSearch(item.Yazar);
            if (normAuthor.Contains(normQuery, StringComparison.Ordinal))
            {
                score += 2.0;
            }
        }

        // 5) İndirme / popülerlik (log ölçekli +0..2)
        if (item.IndirmeSayisi > 0)
        {
            var pop = Math.Log10(item.IndirmeSayisi + 1) / 2.5;
            score += Math.Clamp(pop, 0.0, 2.0);
        }

        // 6) Kapak var (+0.5)
        if (!string.IsNullOrWhiteSpace(item.KapakUrl))
        {
            score += 0.5;
        }

        return score;
    }
}
