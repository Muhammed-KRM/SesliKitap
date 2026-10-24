using MassTransit;
using SesliKitap.Business.Constants;
using SesliKitap.Business.Events;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Consumers;

/// <summary>
/// Python Worker tarafından yayınlanan <see cref="BookExportCompletedEvent"/> olayını tüketerek SignalR üzerinden <c>ExportReady</c> bildirimi yayınlayan tüketici (Faz 8).
/// </summary>
public sealed class BookExportCompletedConsumer : IConsumer<BookExportCompletedEvent>
{
    private readonly INotificationService _notifications;
    private readonly ILogService _logService;

    /// <summary>
    /// <see cref="BookExportCompletedConsumer"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public BookExportCompletedConsumer(
        INotificationService notifications,
        ILogService logService)
    {
        _notifications = notifications;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<BookExportCompletedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var msg = context.Message;

        try
        {
            await _notifications.ExportReadyAsync(
                msg.BookId,
                msg.Format,
                msg.Boyut,
                msg.Basarili,
                msg.HataMesaji,
                context.CancellationToken);
        }
        catch (Exception ex)
        {
            await _logService.LogFunctionErrorAsync(
                ErrorCodes.ConsumerError,
                ex,
                msg,
                msg.TraceId,
                ct: context.CancellationToken);
            throw;
        }
    }
}
