using MassTransit;
using SesliKitap.Business.Constants;
using SesliKitap.Business.Events;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Consumers;

/// <summary>
/// Python Worker'dan gelen <see cref="VoiceProfileReadyEvent"/> olayını işleyen MassTransit tüketicisi.
/// </summary>
public sealed class VoiceProfileReadyConsumer : IConsumer<VoiceProfileReadyEvent>
{
    private readonly IVoiceProfileService _voiceService;
    private readonly ILogService _logService;

    /// <summary>
    /// <see cref="VoiceProfileReadyConsumer"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public VoiceProfileReadyConsumer(
        IVoiceProfileService voiceService,
        ILogService logService)
    {
        _voiceService = voiceService;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<VoiceProfileReadyEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var message = context.Message;

        try
        {
            await _voiceService.HandleReadyAsync(message, context.CancellationToken);
        }
        catch (Exception ex)
        {
            await _logService.LogFunctionErrorAsync(
                ErrorCodes.ConsumerError,
                ex,
                new { message.VoiceProfileId, message.Basarili },
                message.TraceId,
                ct: context.CancellationToken);
            throw;
        }
    }
}
