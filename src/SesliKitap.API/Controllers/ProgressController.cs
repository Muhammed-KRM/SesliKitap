using Microsoft.AspNetCore.Mvc;
using SesliKitap.Business.DTOs.Progress;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Kitap dinleme konumu okuma ve cihazlar arası senkron güncelleme uç noktaları (Bölüm 4.9).
/// </summary>
[Route("api/books/{bookId:guid}/progress")]
public sealed class ProgressController : BaseApiController
{
    private const string SignalRConnectionHeader = "X-SignalR-ConnectionId";
    private readonly IProgressService _progressService;

    /// <summary>
    /// <see cref="ProgressController"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ProgressController(IProgressService progressService)
    {
        _progressService = progressService;
    }

    /// <summary>
    /// Kitabın son dinleme konumunu getirir. Henüz dinlenmemişse 204 No Content döner.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid bookId, CancellationToken ct = default)
    {
        var progress = await _progressService.GetAsync(bookId, ct);
        if (progress is null)
        {
            return NoContent();
        }

        return Ok(progress);
    }

    /// <summary>
    /// Kitabın dinleme konumunu son-yazan-kazanır (<c>istemciZamani</c>) kuralıyla günceller.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(ProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProgressDto>> Update(
        Guid bookId,
        [FromBody] ProgressUpdateRequest request,
        CancellationToken ct = default)
    {
        var senderConnId = Request.Headers.TryGetValue(SignalRConnectionHeader, out var values)
            ? values.FirstOrDefault()
            : null;

        var result = await _progressService.UpdateAsync(bookId, request, senderConnId, ct);
        return Ok(result);
    }
}
