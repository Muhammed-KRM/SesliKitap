using Microsoft.EntityFrameworkCore;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Context;

namespace SesliKitap.API.BackgroundServices;

/// <summary>
/// 3 katmanlı veritabanı loglarını saklama sürelerine (<c>endpoint_logs</c>: 30 gün,
/// <c>pipeline_logs</c>: 90 gün, <c>function_logs</c>: 180 gün) göre periyodik olarak temizleyen arka plan servisi (Faz 9).
/// </summary>
public sealed partial class LogRetentionBackgroundService : BackgroundService
{
    private const int BatchSize = 10_000;
    private static readonly TimeSpan EndpointRetention = TimeSpan.FromDays(30);
    private static readonly TimeSpan PipelineRetention = TimeSpan.FromDays(90);
    private static readonly TimeSpan FunctionRetention = TimeSpan.FromDays(180);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly ILogger<LogRetentionBackgroundService> _logger;

    /// <summary>
    /// <see cref="LogRetentionBackgroundService"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public LogRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IClock clock,
        ILogger<LogRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Süresi dolmuş log kayıtlarını 10.000'lik partiler halinde temizler (birim testlerinden doğrudan çağrılabilir).
    /// </summary>
    public async Task<(int EndpointDeleted, int PipelineDeleted, int FunctionDeleted)> PurgeExpiredLogsOnceAsync(
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = _clock.UtcNow.UtcDateTime;
        var endpointCutoff = now - EndpointRetention;
        var pipelineCutoff = now - PipelineRetention;
        var functionCutoff = now - FunctionRetention;

        var endpointDeleted = await DeleteEndpointLogsAsync(db, endpointCutoff, ct);
        var pipelineDeleted = await DeletePipelineLogsAsync(db, pipelineCutoff, ct);
        var functionDeleted = await DeleteFunctionLogsAsync(db, functionCutoff, ct);

        if (endpointDeleted > 0 || pipelineDeleted > 0 || functionDeleted > 0)
        {
            LogPurgeCompleted(_logger, endpointDeleted, pipelineDeleted, functionDeleted);
        }

        return (endpointDeleted, pipelineDeleted, functionDeleted);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextThreeAmLocal();
            try
            {
                await Task.Delay(delay, stoppingToken);
                await PurgeExpiredLogsOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogCycleError(_logger, ex);
            }
        }
    }

    private static TimeSpan GetDelayUntilNextThreeAmLocal()
    {
        var nowLocal = DateTime.Now;
        var nextRun = nowLocal.Date.AddHours(3);
        if (nextRun <= nowLocal)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun - nowLocal;
    }

    private static async Task<int> DeleteEndpointLogsAsync(AppDbContext db, DateTime cutoff, CancellationToken ct)
    {
        var total = 0;
        while (!ct.IsCancellationRequested)
        {
            int deleted;
            if (db.Database.IsRelational())
            {
                deleted = await db.EndpointLogs
                    .Where(x => x.CreatedAt < cutoff)
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ExecuteDeleteAsync(ct);
            }
            else
            {
                var batch = await db.EndpointLogs
                    .Where(x => x.CreatedAt < cutoff)
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(ct);
                deleted = batch.Count;
                if (deleted > 0)
                {
                    db.EndpointLogs.RemoveRange(batch);
                    await db.SaveChangesAsync(ct);
                }
            }

            total += deleted;
            if (deleted < BatchSize)
            {
                break;
            }
        }

        return total;
    }

    private static async Task<int> DeletePipelineLogsAsync(AppDbContext db, DateTime cutoff, CancellationToken ct)
    {
        var total = 0;
        while (!ct.IsCancellationRequested)
        {
            int deleted;
            if (db.Database.IsRelational())
            {
                deleted = await db.PipelineLogs
                    .Where(x => x.CreatedAt < cutoff)
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ExecuteDeleteAsync(ct);
            }
            else
            {
                var batch = await db.PipelineLogs
                    .Where(x => x.CreatedAt < cutoff)
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(ct);
                deleted = batch.Count;
                if (deleted > 0)
                {
                    db.PipelineLogs.RemoveRange(batch);
                    await db.SaveChangesAsync(ct);
                }
            }

            total += deleted;
            if (deleted < BatchSize)
            {
                break;
            }
        }

        return total;
    }

    private static async Task<int> DeleteFunctionLogsAsync(AppDbContext db, DateTime cutoff, CancellationToken ct)
    {
        var total = 0;
        while (!ct.IsCancellationRequested)
        {
            int deleted;
            if (db.Database.IsRelational())
            {
                deleted = await db.FunctionLogs
                    .Where(x => x.CreatedAt < cutoff)
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ExecuteDeleteAsync(ct);
            }
            else
            {
                var batch = await db.FunctionLogs
                    .Where(x => x.CreatedAt < cutoff)
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(ct);
                deleted = batch.Count;
                if (deleted > 0)
                {
                    db.FunctionLogs.RemoveRange(batch);
                    await db.SaveChangesAsync(ct);
                }
            }

            total += deleted;
            if (deleted < BatchSize)
            {
                break;
            }
        }

        return total;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Süresi dolan log kayıtları temizlendi: Endpoint={EndpointCount}, Pipeline={PipelineCount}, Function={FunctionCount}")]
    private static partial void LogPurgeCompleted(
        ILogger logger,
        int endpointCount,
        int pipelineCount,
        int functionCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "LogRetentionBackgroundService döngü hatası.")]
    private static partial void LogCycleError(ILogger logger, Exception ex);
}
