using SesliKitap.Data.Entities;

namespace SesliKitap.Business.DTOs.Synthesis;

/// <summary>
/// MinIO üzerinde <c>synthesis/{jobId}/input.json</c> olarak saklanan ve Python Worker tarafından okunan iş girdisi şeması (Bölüm 4.7.1).
/// </summary>
public sealed record SynthesisInputPayload(
    Guid JobId,
    SynthesisInputBook Book,
    SynthesisInputVoiceProfile VoiceProfile,
    IReadOnlyList<string> MotorSirasi,
    SynthesisInputSettings Ayarlar,
    IReadOnlyList<SynthesisInputPronunciation> TelaffuzSozlugu,
    IReadOnlyList<SynthesisInputChapter> Chapters);

/// <summary>
/// <see cref="SynthesisInputPayload"/> içindeki kitap meta bilgisi.
/// </summary>
public sealed record SynthesisInputBook(
    Guid Id,
    string Baslik,
    string? Yazar,
    string? KapakYolu);

/// <summary>
/// <see cref="SynthesisInputPayload"/> içindeki ses profili ve klonlama parametreleri.
/// </summary>
public sealed record SynthesisInputVoiceProfile(
    Guid Id,
    string Ad,
    string ReferansSesYolu,
    string ReferansHash,
    string? ReferansMetin,
    VoiceParameters Parametreler);

/// <summary>
/// <see cref="SynthesisInputPayload"/> içindeki duraklama ve kodlama ayarları.
/// </summary>
public sealed record SynthesisInputSettings(
    int CumleArasiMs,
    int ParagrafArasiMs,
    int BaslikSonrasiMs,
    int DiyalogMs,
    bool BolumBasligiOku,
    int OpusBitrateKbps,
    int Mp3BitrateKbps,
    double HedefLufs);

/// <summary>
/// <see cref="SynthesisInputPayload"/> içindeki telaffuz sözlüğü girdisi (Faz 8'de kullanıcı girdileriyle zenginleşir).
/// </summary>
public sealed record SynthesisInputPronunciation(
    string Kelime,
    string Okunus,
    bool BuyukKucukDuyarli);

/// <summary>
/// <see cref="SynthesisInputPayload"/> içindeki seslendirilecek bölüm verisi.
/// </summary>
public sealed record SynthesisInputChapter(
    Guid Id,
    int SiraNo,
    string Baslik,
    int MetinSurumu,
    string TemizMetin,
    int? SesSurumu);
