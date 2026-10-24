using MassTransit;
using SesliKitap.Business.Constants;
using SesliKitap.Business.Events;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Consumers;

/// <summary>
/// Python Worker'dan gelen bölüm sesi hazır (<see cref="ChapterSynthesizedEvent"/>) olayını işleyen tüketici (Bölüm 4.15.3).
/// </summary>
public sealed class ChapterSynthesizedConsumer : IConsumer<ChapterSynthesizedEvent>
{
    private readonly ISynthesisService _synthesisService;
    private readonly ILogService _logService;

    /// <summary>
    /// <see cref="ChapterSynthesizedConsumer"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ChapterSynthesizedConsumer(ISynthesisService synthesisService, ILogService logService)
    {
        _synthesisService = synthesisService;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ChapterSynthesizedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var e = context.Message;

        try
        {
            await _synthesisService.HandleChapterSynthesizedAsync(e, context.CancellationToken);
        }
        catch (Exception ex)
        {
            await _logService.LogFunctionErrorAsync(
                ErrorCodes.ConsumerError,
                ex,
                new { e.JobId, e.BookId, e.ChapterId },
                e.TraceId,
                ct: context.CancellationToken);
            throw;
        }
    }
}
