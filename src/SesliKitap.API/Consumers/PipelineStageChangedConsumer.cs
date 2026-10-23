using MassTransit;
using SesliKitap.Business.Constants;
using SesliKitap.Business.Events;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Services;

namespace SesliKitap.API.Consumers;

/// <summary>
/// Python Worker'dan gelen <see cref="PipelineStageChangedEvent"/> mesajını tüketerek SignalR ve PipelineLog'a ileten tüketici.
/// </summary>
public sealed class PipelineStageChangedConsumer : IConsumer<PipelineStageChangedEvent>
{
    private readonly BookProcessingResultHandler _handler;
    private readonly ILogService _logService;

    /// <summary>
    /// <see cref="PipelineStageChangedConsumer"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public PipelineStageChangedConsumer(BookProcessingResultHandler handler, ILogService logService)
    {
        _handler = handler;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PipelineStageChangedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var e = context.Message;
        try
        {
            await _handler.HandleStageChangedAsync(e, context.CancellationToken);
        }
        catch (Exception ex)
        {
            await _logService.LogFunctionErrorAsync(
                ErrorCodes.ConsumerError,
                ex,
                new { e.BookId, e.JobId, e.Asama, e.Yuzde },
                e.TraceId,
                ct: context.CancellationToken);
            throw;
        }
    }
}
