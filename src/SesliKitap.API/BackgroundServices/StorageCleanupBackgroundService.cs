using SesliKitap.Business.Constants;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Repositories;

namespace SesliKitap.API.BackgroundServices;

/// <summary>
/// Yumuşak silinmiş (soft-deleted) ve üzerinden en az 24 saat geçmiş kitapların MinIO dosyalarını ve veritabanı kayıtlarını kalıcı olarak temizleyen arka plan servisi.
/// </summary>
public sealed partial class StorageCleanupBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly ILogger<StorageCleanupBackgroundService> _logger;

    /// <summary>
    /// <see cref="StorageCleanupBackgroundService"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public StorageCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        IClock clock,
        ILogger<StorageCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Tek bir temizlik turunu çalıştırır (birim testlerinden doğrudan çağrılabilir).
    /// </summary>
    public async Task CleanupExpiredBooksOnceAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var bookRepository = scope.ServiceProvider.GetRequiredService<IBookRepository>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        var logService = scope.ServiceProvider.GetRequiredService<ILogService>();

        var cutoff = _clock.UtcNow.UtcDateTime - RetentionPeriod;
        var expiredBooks = await bookRepository.GetExpiredSoftDeletedBooksAsync(cutoff, ct);

        foreach (var book in expiredBooks)
        {
            try
            {
                await storage.DeleteByPrefixAsync(StorageKeys.BookPrefix(book.Id), ct);
                await bookRepository.HardDeleteAsync(book.Id, ct);
                LogBookCleaned(_logger, book.Id, book.Baslik);
            }
            catch (Exception ex)
            {
                await logService.LogFunctionErrorAsync(
                    ErrorCodes.StorageAccessError,
                    ex,
                    new { BookId = book.Id },
                    ct: ct);
                LogCleanupError(_logger, book.Id, ex);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredBooksOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogCycleError(_logger, ex);
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Silinmiş kitap dosyaları ve veritabanı kaydı kalıcı olarak temizlendi: {BookId} ({Title})")]
    private static partial void LogBookCleaned(ILogger logger, Guid bookId, string title);

    [LoggerMessage(Level = LogLevel.Error, Message = "Silinmiş kitap temizlenirken hata oluştu: {BookId}")]
    private static partial void LogCleanupError(ILogger logger, Guid bookId, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "StorageCleanupBackgroundService döngü hatası.")]
    private static partial void LogCycleError(ILogger logger, Exception ex);
}
