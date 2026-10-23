using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SesliKitap.Business.DTOs.Logs;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Python Worker ve arka plan bileşenlerinden gelen <c>pipeline_logs</c> kayıtlarını
/// veritabanına kalıcı olarak yazan iç (internal) uç nokta.
/// </summary>
[Route("api/internal/logs")]
[DisableRateLimiting]
public sealed class InternalLogsController : BaseApiController
{
    private readonly ILogService _logService;

    /// <summary>
    /// <see cref="InternalLogsController"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="logService">Veritabanı log servisi.</param>
    public InternalLogsController(ILogService logService)
    {
        _logService = logService;
    }

    /// <summary>
    /// Worker bileşeninden gelen pipeline ilerleme/durum kaydını <c>pipeline_logs</c> tablosuna yazar.
    /// </summary>
    /// <param name="request">Pipeline log isteği.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns><c>202 Accepted</c>.</returns>
    [HttpPost("pipeline")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> LogPipeline([FromBody] PipelineLogRequest request, CancellationToken ct)
    {
        await _logService.LogPipelineEventAsync(request, ct);
        return Accepted();
    }
}
