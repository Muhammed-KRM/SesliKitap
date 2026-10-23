using Microsoft.AspNetCore.Mvc;
using SesliKitap.Business.DTOs.Chapter;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Kitap bölümlerini listeleme, metin okuma/düzenleme, birleştirme, bölme, sıralama ve dahil/hariç yönetimi uç noktaları.
/// </summary>
[Route("api")]
public sealed class ChaptersController : BaseApiController
{
    private readonly IChapterService _chapterService;

    /// <summary>
    /// <see cref="ChaptersController"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ChaptersController(IChapterService chapterService)
    {
        _chapterService = chapterService;
    }

    /// <summary>
    /// Belirtilen kitaba ait bölümlerin özet listesini (metinsiz) sıra numarasıyla getirir.
    /// </summary>
    [HttpGet("books/{bookId:guid}/chapters")]
    [ProducesResponseType(typeof(IReadOnlyList<ChapterSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ChapterSummaryDto>>> GetByBook(
        Guid bookId,
        CancellationToken ct = default)
    {
        var result = await _chapterService.GetSummariesByBookAsync(bookId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Bölüm detayını temiz metniyle birlikte getirir.
    /// </summary>
    [HttpGet("chapters/{id:guid}")]
    [ProducesResponseType(typeof(ChapterDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChapterDetailDto>> GetById(
        Guid id,
        CancellationToken ct = default)
    {
        var result = await _chapterService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Bölüm başlığını, temiz metnini ve dahil olma durumunu iyimser kilitleme (<c>metinSurumu</c>) kontrolüyle günceller.
    /// </summary>
    [HttpPut("chapters/{id:guid}")]
    [ProducesResponseType(typeof(ChapterDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChapterDetailDto>> Update(
        Guid id,
        [FromBody] ChapterUpdateRequest request,
        CancellationToken ct = default)
    {
        var result = await _chapterService.UpdateAsync(id, request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Ardışık sıra numaralı bölümleri tek bir bölümde birleştirir ve sonraki bölümleri kaydırır.
    /// </summary>
    [HttpPost("books/{bookId:guid}/chapters/merge")]
    [ProducesResponseType(typeof(ChapterDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChapterDetailDto>> Merge(
        Guid bookId,
        [FromBody] ChapterMergeRequest request,
        CancellationToken ct = default)
    {
        var result = await _chapterService.MergeAsync(bookId, request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Bölümü verilen karakter indeksine en yakın paragraf sınırından ikiye böler.
    /// </summary>
    [HttpPost("chapters/{id:guid}/split")]
    [ProducesResponseType(typeof(IReadOnlyList<ChapterSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<ChapterSummaryDto>>> Split(
        Guid id,
        [FromBody] ChapterSplitRequest request,
        CancellationToken ct = default)
    {
        var result = await _chapterService.SplitAsync(id, request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Kitaptaki tüm bölümlerin sıra numaralarını verilen yeni sıraya göre günceller.
    /// </summary>
    [HttpPut("books/{bookId:guid}/chapters/order")]
    [ProducesResponseType(typeof(IReadOnlyList<ChapterSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<ChapterSummaryDto>>> Reorder(
        Guid bookId,
        [FromBody] ChapterOrderRequest request,
        CancellationToken ct = default)
    {
        var result = await _chapterService.ReorderAsync(bookId, request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Belirtilen bölümlerin seslendirmeye dahil olma durumunu toplu olarak günceller.
    /// </summary>
    [HttpPatch("books/{bookId:guid}/chapters/inclusion")]
    [ProducesResponseType(typeof(IReadOnlyList<ChapterSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ChapterSummaryDto>>> SetInclusion(
        Guid bookId,
        [FromBody] ChapterInclusionRequest request,
        CancellationToken ct = default)
    {
        var result = await _chapterService.SetInclusionAsync(bookId, request, ct);
        return Ok(result);
    }
}
