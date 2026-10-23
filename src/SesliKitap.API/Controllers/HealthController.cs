using Microsoft.AspNetCore.Mvc;
using SesliKitap.API.Middleware;
using SesliKitap.Business.DTOs.Health;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Sistemin ve altyapı bileşenlerinin sağlık durumunu raporlayan uç nokta.
/// </summary>
[Route("api/health")]
public sealed class HealthController : BaseApiController
{
    private readonly IHealthReportService _healthReportService;

    /// <summary>
    /// <see cref="HealthController"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="healthReportService">Sağlık raporu servisi.</param>
    public HealthController(IHealthReportService healthReportService)
    {
        _healthReportService = healthReportService;
    }

    /// <summary>
    /// Sistem sağlık durumunu döner. <c>X-API-Key</c> başlığı yoksa yalnızca genel durumu,
    /// geçerli anahtar varsa tüm bileşenlerin ayrıntılı durumunu döner.
    /// </summary>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Sağlık durumu yanıtı.</returns>
    [HttpGet]
    [AllowAnonymousApiKey]
    [ProducesResponseType(typeof(HealthReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var isAuthenticated = HttpContext.Items.TryGetValue(ApiKeyAuthMiddleware.AuthenticatedItemKey, out var val)
                              && val is true;

        if (!isAuthenticated)
        {
            return Ok(new { status = "Healthy" });
        }

        var report = await _healthReportService.GetDetailedReportAsync(ct);
        return Ok(report);
    }
}
