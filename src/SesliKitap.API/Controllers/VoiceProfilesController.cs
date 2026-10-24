using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SesliKitap.Business.DTOs.Voice;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Ses klonlama profillerini oluşturma, listeleme, güncelleme ve örnek ses akışı sunma uç noktaları (Bölüm 4.9).
/// </summary>
[Route("api/voice-profiles")]
public sealed class VoiceProfilesController : BaseApiController
{
    private readonly IVoiceProfileService _voiceService;

    /// <summary>
    /// <see cref="VoiceProfilesController"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public VoiceProfilesController(IVoiceProfileService voiceService)
    {
        _voiceService = voiceService;
    }

    /// <summary>
    /// Tüm aktif ses profillerini listeler.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<VoiceProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VoiceProfileDto>>> GetList(CancellationToken ct)
    {
        var items = await _voiceService.GetListAsync(ct);
        return Ok(items);
    }

    /// <summary>
    /// Belirtilen kimlikli ses profilinin detaylarını döner.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VoiceProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VoiceProfileDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _voiceService.GetByIdAsync(id, ct);
        return Ok(dto);
    }

    /// <summary>
    /// Referans ses dosyası yükleyerek yeni bir ses profili oluşturur ve hazırlama kuyruğuna gönderir.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(22_000_000)]
    [ProducesResponseType(typeof(VoiceProfileDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VoiceProfileDto>> Create(
        [FromForm] VoiceProfileCreateRequest request,
        CancellationToken ct)
    {
        var dto = await _voiceService.CreateAsync(request, TraceId, ct);
        return AcceptedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    /// <summary>
    /// Ses profilinin adını, açıklamasını ve TTS parametrelerini günceller.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(VoiceProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VoiceProfileDto>> Update(
        Guid id,
        [FromBody] VoiceProfileUpdateRequest request,
        CancellationToken ct)
    {
        var dto = await _voiceService.UpdateAsync(id, request, ct);
        return Ok(dto);
    }

    /// <summary>
    /// Belirtilen ses profilini varsayılan profil olarak işaretler.
    /// </summary>
    [HttpPost("{id:guid}/default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
    {
        await _voiceService.SetDefaultAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Ses profilinin örnek sesini yeniden üretmek üzere kuyruğa gönderir.
    /// </summary>
    [HttpPost("{id:guid}/regenerate-sample")]
    [ProducesResponseType(typeof(VoiceProfileDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VoiceProfileDto>> RegenerateSample(
        Guid id,
        [FromBody] VoiceRegenerateSampleRequest? request,
        CancellationToken ct)
    {
        var dto = await _voiceService.RegenerateSampleAsync(
            id,
            request ?? new VoiceRegenerateSampleRequest(),
            TraceId,
            ct);
        return AcceptedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    /// <summary>
    /// Ses profilinin örnek sesini (<c>sample.ogg</c>) akış olarak döner (medya token veya API anahtarı destekler).
    /// </summary>
    [HttpGet("{id:guid}/sample")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSample(Guid id, CancellationToken ct)
    {
        var (stream, contentType) = await _voiceService.OpenSampleAsync(id, ct);
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(stream, contentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// Ses profilini yumuşak siler.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _voiceService.DeleteAsync(id, ct);
        return NoContent();
    }
}
