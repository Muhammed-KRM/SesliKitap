using MassTransit;
using SesliKitap.Business.Constants;
using SesliKitap.Business.Events;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Consumers;

/// <summary>
/// Python Worker'dan gelen sentez işi tamamlandı (<see cref="SynthesisCompletedEvent"/>) olayını işleyen tüketici (Bölüm 4.15.3).
/// </summary>
public sealed class SynthesisCompletedConsumer : IConsumer<SynthesisCompletedEvent>
{
    private readonly ISynthesisService _synthesisService;
    private readonly ILogService _logService;

    /// <summary>
    /// <see cref="SynthesisCompletedConsumer"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public SynthesisCompletedConsumer(ISynthesisService synthesisService, ILogService logService)
    {
        _synthesisService = synthesisService;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<SynthesisCompletedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var e = context.Message;

        try
        {
            await _synthesisService.HandleCompletedAsync(e, context.CancellationToken);
        }
        catch (Exception ex)
        {
            await _logService.LogFunctionErrorAsync(
                ErrorCodes.ConsumerError,
                ex,
                new { e.JobId, e.BookId, e.Sonuc },
                e.TraceId,
                ct: context.CancellationToken);
            throw;
        }
    }
}
