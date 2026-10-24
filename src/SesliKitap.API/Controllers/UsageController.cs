using Microsoft.AspNetCore.Mvc;
using SesliKitap.Business.DTOs.Usage;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Aylık GPU kullanım süresi, maliyet ve bütçe raporu uç noktası (Faz 8 — Bölüm 4.9).
/// </summary>
[Route("api/usage")]
public sealed class UsageController : BaseApiController
{
    private readonly IUsageService _usageService;

    /// <summary>
    /// <see cref="UsageController"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public UsageController(IUsageService usageService)
    {
        _usageService = usageService;
    }

    /// <summary>
    /// Belirtilen yıl ve ay için GPU/TTS kullanım ve bütçe raporunu döner.
    /// </summary>
    [HttpGet("monthly")]
    [ProducesResponseType(typeof(MonthlyUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MonthlyUsageDto>> GetMonthly(
        [FromQuery] int? yil = null,
        [FromQuery] int? ay = null,
        CancellationToken ct = default)
    {
        var report = await _usageService.GetMonthlyUsageAsync(yil, ay, ct);
        return Ok(report);
    }
}
