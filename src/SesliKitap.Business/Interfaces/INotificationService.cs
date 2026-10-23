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
}
