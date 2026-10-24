using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SesliKitap.Business.DTOs.Book;
using SesliKitap.Business.DTOs.Common;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Kitap yükleme, kütüphane listeleme, detay görüntüleme, güncelleme, silme, yeniden işleme ve kapak/kaynak erişim uç noktaları.
/// </summary>
[Route("api/books")]
public sealed class BooksController : BaseApiController
{
    private const long MaxUploadFormBytes = 315_621_376; // 301 MB (300 MB dosya + multipart başlıkları)
    private readonly IBookService _bookService;
    private readonly IExportService _exportService;

    /// <summary>
    /// <see cref="BooksController"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public BooksController(IBookService bookService, IExportService exportService)
    {
        _bookService = bookService;
        _exportService = exportService;
    }

    /// <summary>
    /// Kütüphanedeki kitapları filtreleme, sıralama ve sayfalama kriterlerine göre listeler.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BookListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BookListDto>>> GetList(
        [FromQuery] BookQuery query,
        CancellationToken ct = default)
    {
        var result = await _bookService.GetListAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// "Dinlemeye devam et" rafı için son dinlenen kitapları getirir.
    /// </summary>
    [HttpGet("continue")]
    [ProducesResponseType(typeof(IReadOnlyList<BookListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookListDto>>> GetContinue(
        [FromQuery] int adet = 10,
        CancellationToken ct = default)
    {
        var result = await _bookService.GetContinueListeningAsync(adet, ct);
        return Ok(result);
    }

    /// <summary>
    /// Kitap detayını bölüm özetleri (metinsiz) ve metin çıkarma istatistikleriyle birlikte getirir.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookDetailDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var detail = await _bookService.GetByIdAsync(id, ct);
        return Ok(detail);
    }

    /// <summary>
    /// Yeni bir PDF, EPUB veya TXT kitap dosyasını yükler ve metin işleme pipeline'ını başlatır.
    /// </summary>
    [HttpPost("upload")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(MaxUploadFormBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadFormBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BookDetailDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookDetailDto>> Upload(
        [FromForm] BookUploadRequest request,
        CancellationToken ct = default)
    {
        var detail = await _bookService.UploadAsync(request, TraceId, ct);
        return AcceptedAtAction(nameof(GetById), new { id = detail.Id }, detail);
    }

    /// <summary>
    /// Kitabın başlık, yazar, açıklama ve varsayılan ses profili bilgilerini günceller.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BookDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookDetailDto>> Update(
        Guid id,
        [FromBody] BookUpdateRequest request,
        CancellationToken ct = default)
    {
        var updated = await _bookService.UpdateAsync(id, request, ct);
        return Ok(updated);
    }

    /// <summary>
    /// Kitabı yumuşak siler (soft delete). Fiziksel dosyalar 24 saat sonra temizlenir.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _bookService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Mevcut bölüm düzenlemelerini silerek kitabın metin çıkarma ve bölümleme pipeline'ını baştan çalıştırır (<c>?onay=true</c> zorunludur).
    /// </summary>
    [HttpPost("{id:guid}/reprocess")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reprocess(
        Guid id,
        [FromQuery] bool onay = false,
        CancellationToken ct = default)
    {
        await _bookService.ReprocessAsync(id, onay, TraceId, ct);
        return Accepted();
    }

    /// <summary>
    /// Kitap kapak görselini (JPEG) döner. Medya token'ı (<c>?t=</c>) veya <c>X-API-Key</c> ile erişilebilir.
    /// </summary>
    [HttpGet("{id:guid}/cover")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCover(Guid id, CancellationToken ct = default)
    {
        var (stream, contentType, etag) = await _bookService.OpenCoverAsync(id, ct);

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, max-age=86400";

        if (Request.Headers.IfNoneMatch.Contains(etag))
        {
            await stream.DisposeAsync();
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return File(stream, contentType, enableRangeProcessing: false);
    }

    /// <summary>
    /// Kitabın orijinal kaynak dosyasını (PDF/EPUB/TXT) indirir.
    /// </summary>
    [HttpGet("{id:guid}/source")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSource(Guid id, CancellationToken ct = default)
    {
        var (stream, contentType, fileName) = await _bookService.OpenSourceAsync(id, ct);
        return File(stream, contentType, fileName);
    }

    /// <summary>
    /// Kitabın hazır bölümlerini, kapak görselini ve <c>playlist.m3u</c> dosyasını ZIP arşivi olarak akıtır.
    /// </summary>
    [HttpGet("{id:guid}/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Export(
        Guid id,
        [FromQuery] string format = "mp3",
        CancellationToken ct = default)
    {
        var zipFileName = await _exportService.GetZipFileNameAsync(id, format, ct);
        Response.ContentType = "application/zip";
        var contentDisposition = new System.Net.Mime.ContentDisposition
        {
            FileName = Uri.EscapeDataString(zipFileName),
            Inline = false
        };
        Response.Headers.ContentDisposition = contentDisposition.ToString();

        await _exportService.WriteZipAsync(id, format, Response.Body, ct);
        return new EmptyResult();
    }
}
