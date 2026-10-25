using Microsoft.AspNetCore.Mvc;
using SesliKitap.Business.DTOs.Bookmark;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Kitap yer imleri (bookmarks) uç noktaları (Faz 8 — Bölüm 4.9).
/// </summary>
[Route("api")]
public sealed class BookmarksController : BaseApiController
{
    private readonly IBookmarkService _bookmarkService;

    /// <summary>
    /// <see cref="BookmarksController"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public BookmarksController(IBookmarkService bookmarkService)
    {
        _bookmarkService = bookmarkService;
    }

    /// <summary>
    /// Belirtilen kitaba ait yer imlerini getirir.
    /// </summary>
    [HttpGet("books/{bookId:guid}/bookmarks")]
    [ProducesResponseType(typeof(IReadOnlyList<BookmarkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BookmarkDto>>> GetByBook(
        Guid bookId,
        CancellationToken ct = default)
    {
        var items = await _bookmarkService.GetByBookAsync(bookId, ct);
        return Ok(items);
    }

    /// <summary>
    /// Belirtilen kitaba yeni bir yer imi ekler.
    /// </summary>
    [HttpPost("books/{bookId:guid}/bookmarks")]
    [ProducesResponseType(typeof(BookmarkDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookmarkDto>> Create(
        Guid bookId,
        [FromBody] BookmarkCreateRequest request,
        CancellationToken ct = default)
    {
        var created = await _bookmarkService.CreateAsync(bookId, request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>
    /// Belirtilen yer imini siler.
    /// </summary>
    [HttpDelete("bookmarks/{id:guid}")]
    [HttpDelete("books/{bookId:guid}/bookmarks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _bookmarkService.DeleteAsync(id, ct);
        return NoContent();
    }
}
