using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SesliKitap.Business.DTOs.Logs;
using SesliKitap.Business.Helpers;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Context;
using SesliKitap.Data.Entities;

namespace SesliKitap.Business.Services;

/// <summary>
/// 3 katmanlı (EndpointLog, FunctionLog, PipelineLog) hata-güvenli (fail-safe) veritabanı loglama servisi.
/// Log yazımı sırasında oluşan hiçbir veritabanı hatası ana iş akışını kesintiye uğratmaz.
/// </summary>
public sealed class LogManager : ILogService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IClock _clock;
    private readonly ILogger<LogManager> _logger;

    /// <summary>
    /// <see cref="LogManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="dbFactory">Kısa ömürlü <see cref="AppDbContext"/> üretici fabrika.</param>
    /// <param name="clock">Sistem saati soyutlaması.</param>
    /// <param name="logger">Yedek (fallback) konsol/dosya günlükleyicisi.</param>
    public LogManager(
        IDbContextFactory<AppDbContext> dbFactory,
        IClock clock,
        ILogger<LogManager> logger)
    {
        _dbFactory = dbFactory;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LogEndpointAsync(EndpointLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);

        try
        {
            log.Query = SensitiveDataMasker.MaskQueryString(log.Query);
            log.RequestBody = SensitiveDataMasker.Mask(log.RequestBody);
            log.ResponseBody = SensitiveDataMasker.Mask(log.ResponseBody);
            if (log.CreatedAt == default)
            {
                log.CreatedAt = _clock.UtcNow.UtcDateTime;
            }

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            db.EndpointLogs.Add(log);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EndpointLog veritabanına yazılamadı: {Method} {Path}", log.Method, log.Path);
        }
    }

    /// <inheritdoc />
    public async Task<long> LogPipelineStartAsync(
        PipelineLogContext ctx,
        string asama,
        string? girdiMetadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        try
        {
            var now = _clock.UtcNow.UtcDateTime;
            var entry = new PipelineLog
            {
                BookId = ctx.BookId,
                JobId = ctx.JobId,
                ChapterId = ctx.ChapterId,
                Asama = asama,
                Durum = "Basladi",
                Yuzde = 0,
                GirdiMetadata = SensitiveDataMasker.EnsureValidJsonb(girdiMetadata),
                Kaynak = string.IsNullOrWhiteSpace(ctx.Kaynak) ? "Api" : ctx.Kaynak,
                TraceId = ctx.TraceId,
                BaslangicZamani = now,
                CreatedAt = now
            };

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            db.PipelineLogs.Add(entry);
            await db.SaveChangesAsync(ct);
            return entry.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PipelineLog başlangıç kaydı yazılamadı: Asama={Asama}, BookId={BookId}", asama, ctx.BookId);
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task LogPipelineEndAsync(long logId, string? ciktiMetadata = null, CancellationToken ct = default)
    {
        if (logId <= 0)
        {
            return;
        }

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var entry = await db.PipelineLogs.FirstOrDefaultAsync(x => x.Id == logId, ct);
            if (entry is null)
            {
                return;
            }

            var now = _clock.UtcNow.UtcDateTime;
            entry.Durum = "Tamamlandi";
            entry.Yuzde = 100;
            entry.BitisZamani = now;
            entry.SureMs = Math.Max(0, (long)(now - entry.BaslangicZamani).TotalMilliseconds);
            entry.CiktiMetadata = SensitiveDataMasker.EnsureValidJsonb(ciktiMetadata);

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PipelineLog bitiş kaydı güncellenemedi: LogId={LogId}", logId);
        }
    }

    /// <inheritdoc />
    public async Task LogPipelineErrorAsync(long logId, Exception ex, CancellationToken ct = default)
    {
        if (logId <= 0 || ex is null)
        {
            return;
        }

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var entry = await db.PipelineLogs.FirstOrDefaultAsync(x => x.Id == logId, ct);
            if (entry is null)
            {
                return;
            }

            var now = _clock.UtcNow.UtcDateTime;
            entry.Durum = "Hata";
            entry.BitisZamani = now;
            entry.SureMs = Math.Max(0, (long)(now - entry.BaslangicZamani).TotalMilliseconds);
            entry.HataMesaji = SensitiveDataMasker.Mask(ex.Message);
            entry.HataDetayi = SensitiveDataMasker.Mask(ex.ToString());

            await db.SaveChangesAsync(ct);
        }
        catch (Exception dbEx)
        {
            _logger.LogError(dbEx, "PipelineLog hata kaydı güncellenemedi: LogId={LogId}", logId);
        }
    }

    /// <inheritdoc />
    public async Task LogPipelineEventAsync(PipelineLogRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Ara "Devam" ilerlemelerinde yalnızca %25 ve katlarını (0, 25, 50, 75, 100) veritabanına yaz
            if (string.Equals(request.Durum, "Devam", StringComparison.OrdinalIgnoreCase) &&
                request.Yuzde.HasValue &&
                request.Yuzde.Value % 25 != 0)
            {
                return;
            }

            var now = _clock.UtcNow.UtcDateTime;
            var isTerminal = string.Equals(request.Durum, "Tamamlandi", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(request.Durum, "Hata", StringComparison.OrdinalIgnoreCase);

            var entry = new PipelineLog
            {
                BookId = request.BookId,
                JobId = request.JobId,
                ChapterId = request.ChapterId,
                Asama = request.Asama,
                Durum = request.Durum,
                Yuzde = request.Yuzde,
                Mesaj = SensitiveDataMasker.Mask(request.Mesaj),
                GirdiMetadata = SensitiveDataMasker.EnsureValidJsonb(request.GirdiMetadata),
                CiktiMetadata = SensitiveDataMasker.EnsureValidJsonb(request.CiktiMetadata),
                HataMesaji = SensitiveDataMasker.Mask(request.HataMesaji),
                HataDetayi = SensitiveDataMasker.Mask(request.HataDetayi),
                Kaynak = string.IsNullOrWhiteSpace(request.Kaynak) ? "Worker" : request.Kaynak,
                TraceId = request.TraceId,
                BaslangicZamani = isTerminal && request.SureMs.HasValue
                    ? now.AddMilliseconds(-Math.Max(0, request.SureMs.Value))
                    : now,
                BitisZamani = isTerminal ? now : null,
                SureMs = request.SureMs,
                CreatedAt = now
            };

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            db.PipelineLogs.Add(entry);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PipelineLog olayı yazılamadı: Asama={Asama}, Durum={Durum}", request.Asama, request.Durum);
        }
    }

    /// <inheritdoc />
    public Task LogFunctionErrorAsync(
        string errorCode,
        Exception ex,
        object? input = null,
        string? traceId = null,
        [CallerMemberName] string methodName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        CancellationToken ct = default)
    {
        return WriteFunctionLogAsync(
            errorCode: errorCode,
            severity: "Error",
            message: ex?.Message,
            stackTrace: ex?.ToString(),
            input: input,
            traceId: traceId,
            methodName: methodName,
            filePath: filePath,
            lineNumber: lineNumber,
            ct: ct);
    }

    /// <inheritdoc />
    public Task LogFunctionWarningAsync(
        string code,
        string message,
        object? input = null,
        string? traceId = null,
        [CallerMemberName] string methodName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        CancellationToken ct = default)
    {
        return WriteFunctionLogAsync(
            errorCode: code,
            severity: "Warning",
            message: message,
            stackTrace: null,
            input: input,
            traceId: traceId,
            methodName: methodName,
            filePath: filePath,
            lineNumber: lineNumber,
            ct: ct);
    }

    private async Task WriteFunctionLogAsync(
        string errorCode,
        string severity,
        string? message,
        string? stackTrace,
        object? input,
        string? traceId,
        string methodName,
        string filePath,
        int lineNumber,
        CancellationToken ct)
    {
        try
        {
            var className = string.IsNullOrWhiteSpace(filePath)
                ? "Unknown"
                : Path.GetFileNameWithoutExtension(filePath);

            string? inputType = null;
            string? inputValue = null;

            if (input is not null)
            {
                inputType = input.GetType().Name;
                try
                {
                    var rawJson = JsonSerializer.Serialize(input);
                    inputValue = SensitiveDataMasker.MaskJson(rawJson);
                }
                catch
                {
                    inputValue = SensitiveDataMasker.Mask(input.ToString());
                }
            }

            var entry = new FunctionLog
            {
                ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "UNKNOWN_ERR" : errorCode,
                ClassName = className,
                MethodName = string.IsNullOrWhiteSpace(methodName) ? "Unknown" : methodName,
                FilePath = filePath,
                LineNumber = lineNumber,
                ErrorMessage = SensitiveDataMasker.Mask(message),
                StackTrace = SensitiveDataMasker.Mask(stackTrace),
                InputType = inputType,
                InputValue = inputValue,
                TraceId = traceId,
                Severity = severity,
                CreatedAt = _clock.UtcNow.UtcDateTime
            };

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            db.FunctionLogs.Add(entry);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception dbEx)
        {
            _logger.LogError(dbEx, "FunctionLog veritabanına yazılamadı: Code={ErrorCode}, Method={Method}", errorCode, methodName);
        }
    }
}
