using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SesliKitap.Business.Constants;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Middleware;

/// <summary>
/// Uygulama genelinde fırlatılan tüm istisnaları yakalayan, <see cref="ProblemDetails"/> (RFC 7807)
/// biçiminde standart JSON yanıtına dönüştüren ve beklenmeyen hataları <c>function_logs</c> tablosuna kaydeden ara katman.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>
    /// <see cref="ExceptionHandlingMiddleware"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="next">Sonraki HTTP işleme delegesi.</param>
    /// <param name="env">Barındırma ortamı bilgisi.</param>
    /// <param name="logger">Yapılandırılmış günlükleyici.</param>
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        IHostEnvironment env,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// HTTP isteğini işler ve oluşan istisnaları ele alır.
    /// </summary>
    /// <param name="context">HTTP bağlamı.</param>
    /// <param name="logService">Veritabanı log servisi.</param>
    public async Task InvokeAsync(HttpContext context, ILogService logService)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logService);

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("İstemci bağlantıyı kapattı: {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, logService);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex, ILogService logService)
    {
        var (statusCode, title, detail, errorCode, existingId) = MapException(ex);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(ex, "İşlenmeyen sunucu hatası ({StatusCode}): {Method} {Path} TraceId={TraceId}",
                statusCode, context.Request.Method, context.Request.Path, context.TraceIdentifier);

            await logService.LogFunctionErrorAsync(
                errorCode ?? ErrorCodes.ApiUnhandledError,
                ex,
                new { Method = context.Request.Method, Path = context.Request.Path.Value },
                context.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning("İstemci/iş kuralı hatası ({StatusCode}): {Message} Path={Path} TraceId={TraceId}",
                statusCode, ex.Message, context.Request.Path, context.TraceIdentifier);
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            problem.Extensions["errorCode"] = errorCode;
        }

        if (existingId.HasValue)
        {
            problem.Extensions["mevcutKaynakId"] = existingId.Value;
        }

        if (ex is ValidationException validationEx)
        {
            var errors = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());
            problem.Extensions["errors"] = errors;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json; charset=utf-8";

        var json = JsonSerializer.Serialize(problem, JsonOptions);
        await context.Response.WriteAsync(json);
    }

    private (int StatusCode, string Title, string Detail, string? ErrorCode, Guid? ExistingId) MapException(Exception ex)
    {
        return ex switch
        {
            ValidationException valEx => (
                StatusCodes.Status400BadRequest,
                "Doğrulama Hatası",
                valEx.Errors.FirstOrDefault()?.ErrorMessage ?? "Geçersiz istek parametreleri.",
                null,
                null),

            BusinessException busEx => (
                StatusCodes.Status400BadRequest,
                "İş Kuralı İhlali",
                busEx.Message,
                busEx.ErrorCode,
                null),

            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                "Kaynak Bulunamadı",
                notFoundEx.Message,
                null,
                null),

            ConflictException conflictEx => (
                StatusCodes.Status409Conflict,
                "Kaynak Çakışması",
                conflictEx.Message,
                null,
                conflictEx.ExistingId),

            ExternalServiceException extEx => (
                StatusCodes.Status502BadGateway,
                "Harici Servis Hatası",
                _env.IsDevelopment() ? extEx.Message : "Harici servis şu anda yanıt vermiyor.",
                ErrorCodes.StorageAccessError,
                null),

            PipelineException pipeEx => (
                StatusCodes.Status500InternalServerError,
                "İş Hattı Hatası",
                _env.IsDevelopment() ? pipeEx.Message : "Beklenmeyen bir hata oluştu.",
                ErrorCodes.ApiUnhandledError,
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Sunucu Hatası",
                _env.IsDevelopment() ? ex.Message : "Beklenmeyen bir hata oluştu.",
                ErrorCodes.ApiUnhandledError,
                null)
        };
    }
}
