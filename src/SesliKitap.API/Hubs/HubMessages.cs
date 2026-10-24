using SesliKitap.Data.Enums;

namespace SesliKitap.API.Hubs;

/// <summary>
/// Kitap metin işleme ilerleme bildirimi yükü (<c>BookProcessingProgress</c>).
/// </summary>
public sealed record BookProcessingProgressMessage(
    Guid BookId,
    string Asama,
    int Yuzde,
    string Mesaj);

/// <summary>
/// Kitap metin işleme tamamlanma bildirimi yükü (<c>BookProcessed</c>).
/// </summary>
public sealed record BookProcessedMessage(
    Guid BookId,
    KitapDurumu Durum,
    int BolumSayisi,
    bool OcrGerekli);

/// <summary>
/// Kitap durum değişimi bildirimi yükü (<c>BookStatusChanged</c>).
/// </summary>
public sealed record BookStatusChangedMessage(
    Guid BookId,
    KitapDurumu Durum);

/// <summary>
/// Pipeline hata bildirimi yükü (<c>PipelineError</c>).
/// </summary>
public sealed record PipelineErrorMessage(
    Guid? BookId,
    Guid? JobId,
    Guid? VoiceProfileId,
    string Asama,
    string HataMesaji,
    bool Kurtarilabilir);

/// <summary>
/// Ses profili hazırlık tamamlanma bildirimi yükü (<c>VoiceProfileReady</c>).
/// </summary>
public sealed record VoiceProfileReadyMessage(
    Guid VoiceProfileId,
    bool Basarili,
    string? HataMesaji);
