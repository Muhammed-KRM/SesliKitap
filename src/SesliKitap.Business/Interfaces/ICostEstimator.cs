namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Kelime sayısı ve GPU çıkarım süresinden ses süresi, GPU süresi ve USD maliyeti hesaplayan servis sözleşmesi (Bölüm 4.15.4).
/// </summary>
public interface ICostEstimator
{
    /// <summary>
    /// Kelime sayısına göre tahmini ses süresini (sn), GPU süresini (sn) ve maliyeti (USD) hesaplar.
    /// </summary>
    /// <param name="words">Toplam kelime sayısı.</param>
    /// <param name="includeColdStart">Soğuk başlatma süresinin dahil edilip edilmeyeceği.</param>
    /// <returns><c>(AudioSec, GpuSec, CostUsd)</c> üçlüsü.</returns>
    (double AudioSec, double GpuSec, decimal CostUsd) Estimate(int words, bool includeColdStart = true);

    /// <summary>
    /// Gerçekleşen GPU çıkarım süresinden (milisaniye) <c>IdleOverheadFactor</c> dahil USD maliyetini hesaplar.
    /// </summary>
    /// <param name="gpuMs">Harcanan GPU çıkarım süresi (ms).</param>
    /// <returns>Hesaplanan maliyet (USD, 4 ondalık basamak).</returns>
    decimal CostFromGpuMs(long gpuMs);
}
