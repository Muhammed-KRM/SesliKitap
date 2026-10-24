using MassTransit;
using SesliKitap.Business.Constants;
using SesliKitap.Business.Events;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Services;

namespace SesliKitap.API.Consumers;

/// <summary>
/// Python Worker tarafından indirilen ve doğrulanan keşif kaynağı (<see cref="BookSourceStoredEvent"/>) mesajını işleyen MassTransit tüketicisi.
/// </summary>
public sealed class BookSourceStoredConsumer : IConsumer<BookSourceStoredEvent>
{
    private readonly BookProcessingResultHandler _handler;
    private readonly ILogService _logService;

    /// <summary>
    /// <see cref="BookSourceStoredConsumer"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public BookSourceStoredConsumer(BookProcessingResultHandler handler, ILogService logService)
    {
        _handler = handler;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<BookSourceStoredEvent> context)
    {
        try
        {
            await _handler.HandleSourceStoredAsync(context.Message, context.CancellationToken);
        }
        catch (Exception ex)
        {
            await _logService.LogFunctionErrorAsync(
                ErrorCodes.ConsumerError,
                ex,
                context.Message,
                context.Message.TraceId,
                ct: context.CancellationToken);
            throw;
        }
    }
}
