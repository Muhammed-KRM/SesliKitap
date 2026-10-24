using Microsoft.AspNetCore.Mvc;
using SesliKitap.Business.DTOs.Pronunciation;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Telaffuz sözlüğü CRUD ve önizleme uç noktaları (Faz 8 — Bölüm 4.9).
/// </summary>
[Route("api/pronunciations")]
public sealed class PronunciationsController : BaseApiController
{
    private readonly IPronunciationService _pronunciationService;

    /// <summary>
    /// <see cref="PronunciationsController"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public PronunciationsController(IPronunciationService pronunciationService)
    {
        _pronunciationService = pronunciationService;
    }

    /// <summary>
    /// Tüm telaffuz sözlüğü girdilerini listeler.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PronunciationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PronunciationDto>>> GetAll(CancellationToken ct = default)
    {
        var items = await _pronunciationService.GetAllAsync(ct);
        return Ok(items);
    }

    /// <summary>
    /// Yeni bir telaffuz sözlüğü girdisi ekler.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PronunciationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PronunciationDto>> Create(
        [FromBody] PronunciationCreateRequest request,
        CancellationToken ct = default)
    {
        var created = await _pronunciationService.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>
    /// Mevcut bir telaffuz sözlüğü girdisini günceller.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PronunciationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PronunciationDto>> Update(
        Guid id,
        [FromBody] PronunciationUpdateRequest request,
        CancellationToken ct = default)
    {
        var updated = await _pronunciationService.UpdateAsync(id, request, ct);
        return Ok(updated);
    }

    /// <summary>
    /// Telaffuz sözlüğü girdisini siler.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _pronunciationService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Verilen metin üzerinde aktif telaffuz sözlüğü kurallarını uygulayarak önizleme üretir.
    /// </summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(PronunciationPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PronunciationPreviewResponse>> Preview(
        [FromBody] PronunciationPreviewRequest request,
        CancellationToken ct = default)
    {
        var preview = await _pronunciationService.PreviewAsync(request, ct);
        return Ok(preview);
    }
}
