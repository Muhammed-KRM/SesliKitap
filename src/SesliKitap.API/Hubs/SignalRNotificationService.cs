using Microsoft.AspNetCore.SignalR;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Enums;

namespace SesliKitap.API.Hubs;

/// <summary>
/// <see cref="INotificationService"/> sözleşmesinin SignalR <see cref="IHubContext{THub, TClient}"/> tabanlı fail-safe uygulaması.
/// </summary>
public sealed partial class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<AudiobookHub, IAudiobookClient> _hubContext;
    private readonly ILogger<SignalRNotificationService> _logger;

    /// <summary>
    /// <see cref="SignalRNotificationService"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public SignalRNotificationService(
        IHubContext<AudiobookHub, IAudiobookClient> hubContext,
        ILogger<SignalRNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task BookProcessingProgressAsync(
        Guid bookId,
        string asama,
        int yuzde,
        string mesaj,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new BookProcessingProgressMessage(bookId, asama, yuzde, mesaj);
            await _hubContext.Clients
                .Groups(HubGroups.Book(bookId), HubGroups.Library)
                .BookProcessingProgress(payload);
        }
        catch (Exception ex)
        {
            LogNotificationWarning(_logger, nameof(BookProcessingProgressAsync), ex);
        }
    }

    /// <inheritdoc />
    public async Task BookProcessedAsync(
        Guid bookId,
        KitapDurumu durum,
        int bolumSayisi,
        bool ocrGerekli,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new BookProcessedMessage(bookId, durum, bolumSayisi, ocrGerekli);
            await _hubContext.Clients
                .Groups(HubGroups.Book(bookId), HubGroups.Library)
                .BookProcessed(payload);
        }
        catch (Exception ex)
        {
            LogNotificationWarning(_logger, nameof(BookProcessedAsync), ex);
        }
    }

    /// <inheritdoc />
    public async Task BookStatusChangedAsync(Guid bookId, KitapDurumu durum, CancellationToken ct = default)
    {
        try
        {
            var payload = new BookStatusChangedMessage(bookId, durum);
            await _hubContext.Clients
                .Group(HubGroups.Library)
                .BookStatusChanged(payload);
        }
        catch (Exception ex)
        {
            LogNotificationWarning(_logger, nameof(BookStatusChangedAsync), ex);
        }
    }

    /// <inheritdoc />
    public async Task PipelineErrorAsync(
        Guid? bookId,
        Guid? jobId,
        Guid? voiceProfileId,
        string asama,
        string hataMesaji,
        bool kurtarilabilir,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new PipelineErrorMessage(bookId, jobId, voiceProfileId, asama, hataMesaji, kurtarilabilir);
            if (bookId.HasValue)
            {
                await _hubContext.Clients
                    .Groups(HubGroups.Book(bookId.Value), HubGroups.Library)
                    .PipelineError(payload);
            }
            else if (voiceProfileId.HasValue)
            {
                await _hubContext.Clients
                    .Group(HubGroups.Voices)
                    .PipelineError(payload);
            }
        }
        catch (Exception ex)
        {
            LogNotificationWarning(_logger, nameof(PipelineErrorAsync), ex);
        }
    }

    /// <inheritdoc />
    public async Task VoiceProfileReadyAsync(
        Guid voiceProfileId,
        bool basarili,
        string? hataMesaji,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new VoiceProfileReadyMessage(voiceProfileId, basarili, hataMesaji);
            await _hubContext.Clients
                .Group(HubGroups.Voices)
                .VoiceProfileReady(payload);
        }
        catch (Exception ex)
        {
            LogNotificationWarning(_logger, nameof(VoiceProfileReadyAsync), ex);
        }
    }

    /// <inheritdoc />
    public async Task SynthesisProgressAsync(
        Guid jobId,
        Guid bookId,
        Guid? chapterId,
        int yuzde,
        int bolumYuzde,
        int tamamlananBolum,
        int toplamBolum,
        string mesaj,
        long gpuMs,
        double? tahminiKalanSn,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new SynthesisProgressMessage(
                jobId,
                bookId,
                chapterId,
                yuzde,
                bolumYuzde,
                tamamlananBolum,
                toplamBolum,
                mesaj,
                gpuMs,
                tahminiKalanSn);
            await _hubContext.Clients
                .Group(HubGroups.Book(bookId))
                .SynthesisProgress(payload);
        }
        catch (Exception ex)
        {
            LogNotificationWarning(_logger, nameof(SynthesisProgressAsync), ex);
        }
    }

    /// <inheritdoc />
    public async Task ChapterReadyAsync(
        Guid jobId,
        Guid bookId,
        Guid chapterId,
        double sureSn,
        string motor,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new ChapterReadyMessage(jobId, bookId, chapterId, sureSn, motor);
            await _hubContext.Clients
                .Group(HubGroups.Book(bookId))
                .ChapterReady(payload);
        }
        catch (Exception ex)
        {
            LogNotificationWarning(_logger, nameof(ChapterReadyAsync), ex);
        }
    }

    /// <inheritdoc />
    public async Task SynthesisCompletedAsync(
        Guid jobId,
        Guid bookId,
        string sonuc,
        int tamamlananBolum,
        int hataliBolum,
        decimal maliyetUsd,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new SynthesisCompletedMessage(
                jobId,
                bookId,
                sonuc,
                tamamlananBolum,
                hataliBolum,
                maliyetUsd);
            await _hubContext.Clients
                .Groups(HubGroups.Book(bookId), HubGroups.Library)
                .SynthesisCompleted(payload);
        }
        catch (Exception ex)
        {
            LogNotificationWarning(_logger, nameof(SynthesisCompletedAsync), ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SignalR bildirimi gönderilemedi ({MethodName}); iş akışı devam ediyor.")]
    private static partial void LogNotificationWarning(ILogger logger, string methodName, Exception ex);
}
