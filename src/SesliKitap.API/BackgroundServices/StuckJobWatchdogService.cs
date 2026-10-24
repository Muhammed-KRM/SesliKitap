using SesliKitap.Business.Constants;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Repositories;

namespace SesliKitap.API.BackgroundServices;

/// <summary>
/// 5 dakikada bir çalışarak 30 dakikadır ilerleme bildirmeyen takılı sentez işlerini yakalayan ve kilidini açan arka plan servisi (Bölüm 4.15.3).
/// </summary>
public sealed partial class StuckJobWatchdogService : BackgroundService
{
    private const string StuckJobReason =
        "30 dakikadır ilerleme yok (worker kapanmış olabilir). 'Yeniden dene' ile kaldığı yerden devam edebilirsiniz.";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly ILogger<StuckJobWatchdogService> _logger;

    /// <summary>
    /// <see cref="StuckJobWatchdogService"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public StuckJobWatchdogService(
        IServiceScopeFactory scopeFactory,
        IClock clock,
        ILogger<StuckJobWatchdogService> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Tek bir watchdog kontrol turunu çalıştırır (birim testlerinden doğrudan çağrılabilir).
    /// </summary>
    public async Task CheckStuckJobsOnceAsync(TimeSpan? thresholdOverride = null, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<ISynthesisJobRepository>();
        var synthesisService = scope.ServiceProvider.GetRequiredService<ISynthesisService>();
        var logService = scope.ServiceProvider.GetRequiredService<ILogService>();

        try
        {
            var threshold = thresholdOverride ?? StuckThreshold;
            var cutoffUtc = _clock.UtcNow.UtcDateTime - threshold;
            var stuckJobs = await jobRepo.GetStuckJobsAsync(cutoffUtc, ct);

            foreach (var job in stuckJobs)
            {
                LogStuckJobDetected(_logger, job.Id, job.BookId);
                await synthesisService.MarkJobFailedAsync(job.Id, StuckJobReason, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Uygulama kapanışı
        }
        catch (Exception ex)
        {
            LogWatchdogError(_logger, ex);
            await logService.LogFunctionErrorAsync(ErrorCodes.ApiUnhandledError, ex, new { Service = nameof(StuckJobWatchdogService) }, ct: ct);
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckStuckJobsOnceAsync(ct: stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Takılı sentez işi tespit edildi ve sonlandırılıyor (JobId={JobId}, BookId={BookId}).")]
    private static partial void LogStuckJobDetected(ILogger logger, Guid jobId, Guid bookId);

    [LoggerMessage(Level = LogLevel.Error, Message = "StuckJobWatchdogService turu sırasında hata oluştu.")]
    private static partial void LogWatchdogError(ILogger logger, Exception ex);
}
