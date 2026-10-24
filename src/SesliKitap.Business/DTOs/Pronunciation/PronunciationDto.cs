namespace SesliKitap.Business.DTOs.Pronunciation;

/// <summary>
/// Telaffuz sözlüğü girdisi görüntüleme modeli (Faz 8).
/// </summary>
public sealed record PronunciationDto(
    Guid Id,
    string Kelime,
    string Okunus,
    bool BuyukKucukDuyarli,
    bool AktifMi,
    string? Aciklama,
    DateTime OlusturmaTarihi);

/// <summary>
/// Yeni telaffuz sözlüğü girdisi oluşturma isteği.
/// </summary>
public sealed record PronunciationCreateRequest(
    string Kelime,
    string Okunus,
    bool BuyukKucukDuyarli = false,
    bool AktifMi = true,
    string? Aciklama = null);

/// <summary>
/// Mevcut telaffuz sözlüğü girdisi güncelleme isteği.
/// </summary>
public sealed record PronunciationUpdateRequest(
    string Kelime,
    string Okunus,
    bool BuyukKucukDuyarli = false,
    bool AktifMi = true,
    string? Aciklama = null);

/// <summary>
/// Telaffuz sözlüğü önizleme isteği.
/// </summary>
public sealed record PronunciationPreviewRequest(string Metin);

/// <summary>
/// Telaffuz sözlüğü önizleme yanıtı.
/// </summary>
public sealed record PronunciationPreviewResponse(
    string OrijinalMetin,
    string OnizlemeMetni,
    int DegisenKelimeSayisi);
