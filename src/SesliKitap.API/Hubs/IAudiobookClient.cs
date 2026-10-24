namespace SesliKitap.API.Hubs;

/// <summary>
/// <see cref="AudiobookHub"/> üzerinden istemcilere gönderilen güçlü tipli olay sözleşmesi (Bölüm 4.10).
/// </summary>
public interface IAudiobookClient
{
    /// <summary>
    /// Kitap metin işleme aşama ilerlemesi olayı.
    /// </summary>
    Task BookProcessingProgress(BookProcessingProgressMessage message);

    /// <summary>
    /// Kitap metin işleme tamamlanma olayı.
    /// </summary>
    Task BookProcessed(BookProcessedMessage message);

    /// <summary>
    /// Kitap durumu değişim olayı.
    /// </summary>
    Task BookStatusChanged(BookStatusChangedMessage message);

    /// <summary>
    /// Pipeline hata olayı.
    /// </summary>
    Task PipelineError(PipelineErrorMessage message);

    /// <summary>
    /// Ses profili hazırlama tamamlanma olayı.
    /// </summary>
    Task VoiceProfileReady(VoiceProfileReadyMessage message);
}
