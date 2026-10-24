using SesliKitap.Data.Enums;

namespace SesliKitap.Business.DTOs.Voice;

/// <summary>
/// Ses profili TTS model parametreleri veri transfer nesnesi.
/// </summary>
public sealed record VoiceParametersDto(
    double CfgValue = 2.0,
    int InferenceTimesteps = 10,
    double KonusmaHizi = 1.0,
    int? Tohum = null);

/// <summary>
/// Ses profili detay ve liste veri transfer nesnesi.
/// </summary>
public sealed record VoiceProfileDto(
    Guid Id,
    string Ad,
    string? Aciklama,
    Cinsiyet? Cinsiyet,
    string? ReferansMetin,
    double? ReferansSureSn,
    bool OrnekSesVar,
    VoiceParametersDto Parametreler,
    bool VarsayilanMi,
    SesProfiliDurumu Durum,
    string? HataMesaji,
    DateTime OlusturmaTarihi);
