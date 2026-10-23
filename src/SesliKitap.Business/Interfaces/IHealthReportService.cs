using SesliKitap.Business.DTOs.Health;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Sistem bileşenlerinin sağlık durumunu derleyen servis sözleşmesi.
/// </summary>
public interface IHealthReportService
{
    /// <summary>
    /// Kayıtlı sağlık kontrollerini çalıştırarak ayrıntılı sistem sağlık raporunu oluşturur.
    /// </summary>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Sistem sağlık raporu.</returns>
    Task<HealthReportDto> GetDetailedReportAsync(CancellationToken ct = default);
}
