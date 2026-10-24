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

/// <summary>
/// Seslendirme (sentez) işi ilerleme bildirimi yükü (<c>SynthesisProgress</c>).
/// </summary>
public sealed record SynthesisProgressMessage(
    Guid JobId,
    Guid BookId,
    Guid? ChapterId,
    int Yuzde,
    int BolumYuzde,
    int TamamlananBolum,
    int ToplamBolum,
    string Mesaj,
    long GpuMs,
    double? TahminiKalanSn);

/// <summary>
/// Bölüm sesi hazır bildirimi yükü (<c>ChapterReady</c>).
/// </summary>
public sealed record ChapterReadyMessage(
    Guid JobId,
    Guid BookId,
    Guid ChapterId,
    double SureSn,
    string Motor);

/// <summary>
/// Sentez işi tamamlanma bildirimi yükü (<c>SynthesisCompleted</c>).
/// </summary>
public sealed record SynthesisCompletedMessage(
    Guid JobId,
    Guid BookId,
    string Sonuc,
    int TamamlananBolum,
    int HataliBolum,
    decimal MaliyetUsd);

/// <summary>
/// Cihazlar arası dinleme konumu güncelleme bildirimi yükü (<c>ProgressUpdated</c>).
/// </summary>
public sealed record ProgressUpdatedMessage(
    Guid BookId,
    Guid ChapterId,
    int BolumSiraNo,
    double PozisyonSn,
    double OynatmaHizi,
    string CihazKimligi,
    string? CihazAdi,
    DateTime IstemciZamani);

/// <summary>
/// M4B dışa aktarım hazır bildirimi yükü (<c>ExportReady</c>) (Faz 8).
/// </summary>
public sealed record ExportReadyMessage(
    Guid BookId,
    string Format,
    long Boyut,
    bool Basarili = true,
    string? HataMesaji = null);
