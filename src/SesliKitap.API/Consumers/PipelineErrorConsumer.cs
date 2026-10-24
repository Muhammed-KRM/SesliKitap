using MassTransit;
using SesliKitap.Business.Constants;
using SesliKitap.Business.Events;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Services;

namespace SesliKitap.API.Consumers;

/// <summary>
/// Python Worker'dan gelen <see cref="PipelineErrorEvent"/> mesajını tüketerek kitap durumunu güncelleyen ve SignalR bildirimi yayınlayan tüketici.
/// </summary>
public sealed class PipelineErrorConsumer : IConsumer<PipelineErrorEvent>
{
    private readonly BookProcessingResultHandler _handler;
    private readonly ISynthesisService? _synthesisService;
    private readonly ILogService _logService;

    /// <summary>
    /// <see cref="PipelineErrorConsumer"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public PipelineErrorConsumer(
        BookProcessingResultHandler handler,
        ILogService logService,
        ISynthesisService? synthesisService = null)
    {
        _handler = handler;
        _logService = logService;
        _synthesisService = synthesisService;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PipelineErrorEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var e = context.Message;
        try
        {
            if (e.JobId.HasValue && _synthesisService is not null)
            {
                await _synthesisService.HandleErrorAsync(e, context.CancellationToken);
            }
            else
            {
                await _handler.HandleErrorAsync(e, context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            await _logService.LogFunctionErrorAsync(
                ErrorCodes.ConsumerError,
                ex,
                new { e.BookId, e.JobId, e.Asama, e.HataKodu },
                e.TraceId,
                ct: context.CancellationToken);
            throw;
        }
    }
}
