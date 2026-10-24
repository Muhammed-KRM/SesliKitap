using SesliKitap.Business.DTOs.Usage;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Aylık GPU/TTS kullanım istatistiklerini ve bütçe durumunu raporlayan iş servisi sözleşmesi (Faz 8).
/// </summary>
public interface IUsageService
{
    /// <summary>
    /// Belirtilen yıl ve ay (verilmezse içinde bulunulan UTC ay) için GPU süresi, maliyet, üretilen ses saati ve son 6 ay geçmişini döner.
    /// </summary>
    Task<MonthlyUsageDto> GetMonthlyUsageAsync(int? year = null, int? month = null, CancellationToken ct = default);
}
