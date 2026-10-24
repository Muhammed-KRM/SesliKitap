using SesliKitap.Data.Enums;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// İstemcilere canlı bildirim (SignalR) gönderen servis sözleşmesi (Bölüm 4.10).
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Kitap metin işleme ilerlemesini bildirir (<c>BookProcessingProgress</c>).
    /// </summary>
    Task BookProcessingProgressAsync(Guid bookId, string asama, int yuzde, string mesaj, CancellationToken ct = default);

    /// <summary>
    /// Kitap metin işleme tamamlandığında bildirir (<c>BookProcessed</c>).
    /// </summary>
    Task BookProcessedAsync(Guid bookId, KitapDurumu durum, int bolumSayisi, bool ocrGerekli, CancellationToken ct = default);

    /// <summary>
    /// Kitap durumunun değiştiğini kütüphane grubuna bildirir (<c>BookStatusChanged</c>).
    /// </summary>
    Task BookStatusChangedAsync(Guid bookId, KitapDurumu durum, CancellationToken ct = default);

    /// <summary>
    /// Pipeline hata olayını ilgili kitap veya ses profili grubuna bildirir (<c>PipelineError</c>).
    /// </summary>
    Task PipelineErrorAsync(
        Guid? bookId,
        Guid? jobId,
        Guid? voiceProfileId,
        string asama,
        string hataMesaji,
        bool kurtarilabilir,
        CancellationToken ct = default);

    /// <summary>
    /// Ses profili hazırlama ve örnek ses üretimi tamamlandığında bildirir (<c>VoiceProfileReady</c>).
    /// </summary>
    Task VoiceProfileReadyAsync(
        Guid voiceProfileId,
        bool basarili,
        string? hataMesaji,
        CancellationToken ct = default);

    /// <summary>
    /// Seslendirme (sentez) işi ilerlemesini bildirir (<c>SynthesisProgress</c>).
    /// </summary>
    Task SynthesisProgressAsync(
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
        CancellationToken ct = default);

    /// <summary>
    /// Bir bölümün seslendirmesi tamamlandığında bildirir (<c>ChapterReady</c>).
    /// </summary>
    Task ChapterReadyAsync(
        Guid jobId,
        Guid bookId,
        Guid chapterId,
        double sureSn,
        string motor,
        CancellationToken ct = default);

    /// <summary>
    /// Seslendirme (sentez) işi tamamlandığında bildirir (<c>SynthesisCompleted</c>).
    /// </summary>
    Task SynthesisCompletedAsync(
        Guid jobId,
        Guid bookId,
        string sonuc,
        int tamamlananBolum,
        int hataliBolum,
        decimal maliyetUsd,
        CancellationToken ct = default);

    /// <summary>
    /// Dinleme konumu güncellendiğinde gönderen bağlantı dışındaki istemcilere bildirir (<c>ProgressUpdated</c>).
    /// </summary>
    Task ProgressUpdatedAsync(
        Guid bookId,
        Guid chapterId,
        int bolumSiraNo,
        double pozisyonSn,
        double oynatmaHizi,
        string cihazKimligi,
        string? cihazAdi,
        DateTime istemciZamani,
        string? senderConnectionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// M4B sesli kitap dışa aktarım dosyası hazır olduğunda ilgili kitap grubuna bildirir (<c>ExportReady</c>) (Faz 8).
    /// </summary>
    Task ExportReadyAsync(
        Guid bookId,
        string format,
        long boyut,
        bool basarili = true,
        string? hataMesaji = null,
        CancellationToken ct = default);
}
