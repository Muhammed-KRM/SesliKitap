using MassTransit;
using SesliKitap.Business.Constants;
using SesliKitap.Business.Events;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Services;

namespace SesliKitap.API.Consumers;

/// <summary>
/// Python Worker'dan gelen <see cref="BookProcessedEvent"/> mesajını tüketerek bölümleri veritabanına kaydeden tüketici.
/// </summary>
public sealed class BookProcessedConsumer : IConsumer<BookProcessedEvent>
{
    private readonly BookProcessingResultHandler _handler;
    private readonly ILogService _logService;

    /// <summary>
    /// <see cref="BookProcessedConsumer"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public BookProcessedConsumer(BookProcessingResultHandler handler, ILogService logService)
    {
        _handler = handler;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<BookProcessedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var e = context.Message;
        try
        {
            await _handler.HandleBookProcessedAsync(e, context.CancellationToken);
        }
        catch (Exception ex)
        {
            await _logService.LogFunctionErrorAsync(
                ErrorCodes.BookProcessedHandlerError,
                ex,
                new { e.BookId, e.BolumlerYolu, e.OcrGerekli },
                e.TraceId,
                ct: context.CancellationToken);
            throw;
        }
    }
}
