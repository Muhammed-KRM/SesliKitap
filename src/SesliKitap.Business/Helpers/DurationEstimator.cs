namespace SesliKitap.Business.Helpers;

/// <summary>
/// Kelime sayısından tahmini ses süresi, GPU çıkarım süresi ve USD maliyeti hesaplayan saf yardımcı sınıf.
/// </summary>
public static class DurationEstimator
{
    /// <summary>
    /// Varsayılan dakikadaki Türkçe okuma hızı (kelime/dakika).
    /// </summary>
    public const int DefaultWordsPerMinute = 140;

    /// <summary>
    /// Kelime sayısından tahmini ses süresini (saniye) hesaplar.
    /// </summary>
    /// <param name="words">Kelime sayısı.</param>
    /// <param name="wordsPerMinute">Dakikadaki kelime sayısı (varsayılan 140).</param>
    /// <returns>Tahmini ses süresi (saniye).</returns>
    public static double EstimateAudioSeconds(int words, int wordsPerMinute = DefaultWordsPerMinute)
    {
        if (words <= 0 || wordsPerMinute <= 0)
        {
            return 0d;
        }

        return Math.Round((double)words / wordsPerMinute * 60d, 1);
    }

    /// <summary>
    /// Tahmini ses süresinden GPU çalışma süresini (saniye) hesaplar.
    /// </summary>
    /// <param name="audioSeconds">Tahmini ses süresi (saniye).</param>
    /// <param name="rtf">Gerçek zaman çarpanı (Real-Time Factor, örn. 0.20).</param>
    /// <param name="coldStartSeconds">Soğuk başlatma süresi (saniye).</param>
    /// <param name="idleOverheadFactor">Boşta bekleme/parti arası ek süre çarpanı (örn. 1.15).</param>
    /// <returns>Tahmini GPU süresi (saniye).</returns>
    public static double EstimateGpuSeconds(
        double audioSeconds,
        double rtf = 0.20,
        int coldStartSeconds = 90,
        double idleOverheadFactor = 1.15)
    {
        if (audioSeconds <= 0)
        {
            return 0d;
        }

        return Math.Round((audioSeconds * rtf + coldStartSeconds) * idleOverheadFactor, 1);
    }

    /// <summary>
    /// GPU süresinden (saniye) tahmini USD maliyetini 4 ondalık basamak hassasiyetle hesaplar.
    /// </summary>
    /// <param name="gpuSeconds">GPU çalışma süresi (saniye).</param>
    /// <param name="pricePerHourUsd">Saatlik GPU birim fiyatı (USD).</param>
    /// <returns>Tahmini maliyet (USD).</returns>
    public static decimal EstimateCostUsd(double gpuSeconds, decimal pricePerHourUsd = 0.80m)
    {
        if (gpuSeconds <= 0 || pricePerHourUsd <= 0)
        {
            return 0m;
        }

        var cost = (decimal)gpuSeconds / 3600m * pricePerHourUsd;
        return Math.Round(cost, 4, MidpointRounding.AwayFromZero);
    }
}
