using Microsoft.AspNetCore.Mvc;
using SesliKitap.Business.DTOs.Common;
using SesliKitap.Business.DTOs.Synthesis;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Enums;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Kitap seslendirme (sentez) tahmini, başlatma, iptal, yeniden deneme ve iş listeleme uç noktaları (Bölüm 4.9).
/// </summary>
[Route("api")]
public sealed class SynthesisController : BaseApiController
{
    private readonly ISynthesisService _synthesisService;

    /// <summary>
    /// <see cref="SynthesisController"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public SynthesisController(ISynthesisService synthesisService)
    {
        _synthesisService = synthesisService;
    }

    /// <summary>
    /// Belirtilen kitap ve ses profili için tahmini ses süresi, GPU süresi, maliyet ve aylık bütçe durumunu hesaplar.
    /// </summary>
    [HttpGet("books/{bookId:guid}/synthesis/estimate")]
    [ProducesResponseType(typeof(SynthesisEstimateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SynthesisEstimateDto>> Estimate(
        Guid bookId,
        [FromQuery] Guid voiceProfileId,
        [FromQuery] Guid[]? chapterIds = null,
        [FromQuery] bool sadeceEksikler = true,
        CancellationToken ct = default)
    {
        var estimate = await _synthesisService.EstimateAsync(
            bookId,
            voiceProfileId,
            chapterIds,
            sadeceEksikler,
            ct);
        return Ok(estimate);
    }

    /// <summary>
    /// Kitap için seslendirme (sentez) işi başlatır.
    /// </summary>
    [HttpPost("books/{bookId:guid}/synthesis")]
    [ProducesResponseType(typeof(SynthesisJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SynthesisJobDto>> Start(
        Guid bookId,
        [FromBody] SynthesisStartRequest request,
        CancellationToken ct = default)
    {
        var job = await _synthesisService.StartAsync(bookId, request, TraceId, ct);
        return AcceptedAtAction(nameof(GetById), new { jobId = job.Id }, job);
    }

    /// <summary>
    /// Sentez işlerini durum filtresi ve sayfalamayla listeler.
    /// </summary>
    [HttpGet("synthesis")]
    [ProducesResponseType(typeof(PagedResult<SynthesisJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SynthesisJobDto>>> GetList(
        [FromQuery] SentezIsDurumu? durum = null,
        [FromQuery] int sayfa = 1,
        [FromQuery] int boyut = 20,
        CancellationToken ct = default)
    {
        var result = await _synthesisService.GetListAsync(durum, new PageQuery { Sayfa = sayfa, Boyut = boyut }, ct);
        return Ok(result);
    }

    /// <summary>
    /// Sentez işi detayını bölüm durumlarıyla birlikte getirir.
    /// </summary>
    [HttpGet("synthesis/{jobId:guid}")]
    [ProducesResponseType(typeof(SynthesisJobDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SynthesisJobDetailDto>> GetById(
        Guid jobId,
        CancellationToken ct = default)
    {
        var detail = await _synthesisService.GetByIdAsync(jobId, ct);
        return Ok(detail);
    }

    /// <summary>
    /// Devam eden veya kuyruktaki sentez işini iptal eder.
    /// </summary>
    [HttpPost("synthesis/{jobId:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid jobId, CancellationToken ct = default)
    {
        await _synthesisService.CancelAsync(jobId, ct);
        return Accepted();
    }

    /// <summary>
    /// Hatalı, iptal edilmiş veya kısmen tamamlanmış işi eksik bölümlerle yeniden başlatır.
    /// </summary>
    [HttpPost("synthesis/{jobId:guid}/retry")]
    [ProducesResponseType(typeof(SynthesisJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SynthesisJobDto>> Retry(Guid jobId, CancellationToken ct = default)
    {
        var job = await _synthesisService.RetryAsync(jobId, TraceId, ct);
        return AcceptedAtAction(nameof(GetById), new { jobId = job.Id }, job);
    }
}
