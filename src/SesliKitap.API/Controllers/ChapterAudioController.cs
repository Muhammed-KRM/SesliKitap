using Microsoft.AspNetCore.Mvc;
using SesliKitap.API.Streaming;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Bölüm ses akışı (HTTP 206 Partial Content / Range) ve cümle zaman damgalarını sunan denetleyici (Bölüm 4.15.3).
/// </summary>
[Route("api/chapters")]
public sealed partial class ChapterAudioController : BaseApiController
{
    private readonly IChapterAudioService _audioService;
    private readonly ILogger<ChapterAudioController> _logger;

    /// <summary>
    /// <see cref="ChapterAudioController"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ChapterAudioController(
        IChapterAudioService audioService,
        ILogger<ChapterAudioController> logger)
    {
        _audioService = audioService;
        _logger = logger;
    }

    /// <summary>
    /// Bölüm ses dosyasını HTTP Range (<c>206 Partial Content</c>) ve <c>ETag</c> desteğiyle akıtır.
    /// </summary>
    /// <param name="id">Bölüm kimliği.</param>
    /// <param name="format">İstenen ses formatı (<c>opus</c> veya <c>mp3</c>).</param>
    /// <param name="ct">İptal belirteci.</param>
    [HttpGet("{id:guid}/audio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status416RangeNotSatisfiable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task Stream(
        Guid id,
        [FromQuery] string format = "opus",
        CancellationToken ct = default)
    {
        var info = await _audioService.GetAudioInfoAsync(id, format, ct);

        Response.Headers.AcceptRanges = "bytes";
        Response.Headers.ETag = info.ETag;
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";

        if (Request.Headers.IfNoneMatch.Count > 0 &&
            string.Equals(Request.Headers.IfNoneMatch.ToString().Trim(), info.ETag, StringComparison.Ordinal))
        {
            Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        var range = RangeRequestParser.Parse(Request.Headers.Range, info.Length);
        if (range is { IsSatisfiable: false })
        {
            Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
            Response.Headers.ContentRange = $"bytes */{info.Length}";
            return;
        }

        var (offset, length) = range is null
            ? (0L, info.Length)
            : (range.Start, range.Length);

        Response.StatusCode = range is null
            ? StatusCodes.Status200OK
            : StatusCodes.Status206PartialContent;

        if (range is not null)
        {
            Response.Headers.ContentRange = $"bytes {range.Start}-{range.End}/{info.Length}";
        }

        Response.ContentType = info.ContentType;
        Response.ContentLength = length;

        try
        {
            await _audioService.CopyRangeToAsync(info.ObjectKey, offset, length, Response.Body, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            LogClientAbortedStream(_logger, id);
        }
    }

    /// <summary>
    /// Bölümün cümle zaman damgaları (<c>timestamps.json</c>) verisini döndürür.
    /// </summary>
    /// <param name="id">Bölüm kimliği.</param>
    /// <param name="ct">İptal belirteci.</param>
    [HttpGet("{id:guid}/timestamps")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimestamps(Guid id, CancellationToken ct = default)
    {
        var json = await _audioService.GetTimestampsJsonAsync(id, ct);
        return Content(json, "application/json; charset=utf-8");
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "İstemci bölüm ses akışı bağlantısını kapattı (ChapterId={ChapterId}).")]
    private static partial void LogClientAbortedStream(ILogger logger, Guid chapterId);
}
