using System.Diagnostics;
using System.Text;
using SesliKitap.Business.Helpers;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Entities;

namespace SesliKitap.API.Middleware;

/// <summary>
/// HTTP istek ve yanıtlarını yakalayıp maskeleyerek <c>endpoint_logs</c> tablosuna kaydeden ara katman (Katman 1).
/// Büyük dosya yüklemelerinde ve medya akışı (<c>Range</c>) uç noktalarında tamponlama yapmaz.
/// </summary>
public sealed class RequestResponseLoggingMiddleware
{
    private const int MaxBodyLogLength = 4096;

    private static readonly string[] StreamPathSuffixes =
    [
        "/audio",
        "/cover",
        "/sample",
        "/source",
        "/export"
    ];

    private readonly RequestDelegate _next;

    /// <summary>
    /// <see cref="RequestResponseLoggingMiddleware"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="next">Sonraki HTTP işleme delegesi.</param>
    public RequestResponseLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// İstek ve yanıtı ölçüp <see cref="ILogService"/> üzerinden kaydeder.
    /// </summary>
    /// <param name="context">HTTP bağlamı.</param>
    /// <param name="logService">Veritabanı log servisi.</param>
    public async Task InvokeAsync(HttpContext context, ILogService logService)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logService);

        var path = context.Request.Path.Value ?? string.Empty;

        // SignalR WebSocket, Swagger statik dosyaları ve CORS preflight isteklerini tamamen atla
        if (HttpMethods.IsOptions(context.Request.Method) ||
            path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Kendi kendini loglama döngüsünü önlemek için /api/internal/logs uç noktasında gövdeyi tamponlama
        var isInternalLogEndpoint = path.StartsWith("/api/internal/logs", StringComparison.OrdinalIgnoreCase);
        var isMultipart = context.Request.HasFormContentType ||
                          (context.Request.ContentType?.Contains("multipart/", StringComparison.OrdinalIgnoreCase) ?? false);
        var isStreamingEndpoint = StreamPathSuffixes.Any(s => path.EndsWith(s, StringComparison.OrdinalIgnoreCase));

        var skipBodyBuffering = isInternalLogEndpoint || isMultipart || isStreamingEndpoint;

        var stopwatch = Stopwatch.StartNew();
        string? requestBodyText = null;
        string? responseBodyText = null;

        if (!skipBodyBuffering)
        {
            requestBodyText = await ReadRequestBodyAsync(context.Request);
        }
        else if (isMultipart)
        {
            requestBodyText = "[MULTIPART_FORM_DATA]";
        }

        var originalResponseBody = context.Response.Body;
        using var responseBuffer = skipBodyBuffering ? null : new MemoryStream();
        if (responseBuffer is not null)
        {
            context.Response.Body = responseBuffer;
        }

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            if (responseBuffer is not null)
            {
                responseBuffer.Position = 0;
                responseBodyText = await ReadStreamPreviewAsync(responseBuffer);
                responseBuffer.Position = 0;

                if (context.Response.StatusCode != StatusCodes.Status204NoContent &&
                    context.Response.StatusCode != StatusCodes.Status304NotModified &&
                    responseBuffer.Length > 0)
                {
                    await responseBuffer.CopyToAsync(originalResponseBody);
                }

                context.Response.Body = originalResponseBody;
            }
            else if (isStreamingEndpoint)
            {
                responseBodyText = "[BINARY_STREAM]";
            }

            if (!isInternalLogEndpoint)
            {
                var logEntry = new EndpointLog
                {
                    TraceId = context.TraceIdentifier,
                    Method = context.Request.Method,
                    Path = Truncate(path, 500) ?? "/",
                    Query = Truncate(SensitiveDataMasker.MaskQueryString(context.Request.QueryString.Value), 2000),
                    RequestBody = Truncate(SensitiveDataMasker.Mask(requestBodyText), MaxBodyLogLength),
                    ResponseBody = Truncate(SensitiveDataMasker.Mask(responseBodyText), MaxBodyLogLength),
                    StatusCode = context.Response.StatusCode,
                    IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 500),
                    DurationMs = stopwatch.ElapsedMilliseconds
                };

                await logService.LogEndpointAsync(logEntry);
            }
        }
    }

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or <= 0)
        {
            return null;
        }

        request.EnableBuffering();
        request.Body.Position = 0;
        var text = await ReadStreamPreviewAsync(request.Body);
        request.Body.Position = 0;
        return text;
    }

    private static async Task<string?> ReadStreamPreviewAsync(Stream stream)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        var buffer = new char[MaxBodyLogLength];
        var read = await reader.ReadBlockAsync(buffer, 0, MaxBodyLogLength);
        if (read <= 0)
        {
            return null;
        }

        var result = new string(buffer, 0, read);
        return stream.Length > MaxBodyLogLength ? result + "...[TRUNCATED]" : result;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
