using SesliKitap.Data.Enums;

namespace SesliKitap.Business.DTOs.Synthesis;

/// <summary>
/// Sentez işi özet görünüm modeli (Bölüm 4.15.4).
/// </summary>
/// <param name="Id">Sentez işi kimliği.</param>
/// <param name="BookId">Kitap kimliği.</param>
/// <param name="KitapBaslik">Kitap başlığı.</param>
/// <param name="VoiceProfileId">Ses profili kimliği.</param>
/// <param name="SesProfiliAd">Ses profili adı.</param>
/// <param name="Durum">İşin mevcut durumu.</param>
/// <param name="Yuzde">Genel tamamlanma yüzdesi (0–100).</param>
/// <param name="ToplamBolum">Toplam hedef bölüm sayısı.</param>
/// <param name="TamamlananBolum">Tamamlanan bölüm sayısı.</param>
/// <param name="HataliBolum">Hata alan bölüm sayısı.</param>
/// <param name="MevcutBolumId">Şu an işlenen bölüm kimliği.</param>
/// <param name="Mesaj">Son ilerleme mesajı.</param>
/// <param name="MotorSirasi">Motor öncelik sırası.</param>
/// <param name="KullanilanMotorlar">Fiilen kullanılan motorlar.</param>
/// <param name="GpuMs">Harcanan GPU süresi (ms).</param>
/// <param name="TahminiMaliyetUsd">Tahmini/gerçekleşen maliyet (USD).</param>
/// <param name="TahminiSureSn">Tahmini ses süresi (sn).</param>
/// <param name="UretilenSesSn">Üretilen ses süresi (sn).</param>
/// <param name="OnbellektenParca">Önbellekten gelen parça sayısı.</param>
/// <param name="UretilenParca">GPU'da üretilen parça sayısı.</param>
/// <param name="HataMesaji">Varsa hata mesajı.</param>
/// <param name="OlusturmaTarihi">Oluşturulma zamanı (UTC).</param>
/// <param name="BaslangicZamani">Başlangıç zamanı (UTC).</param>
/// <param name="BitisZamani">Bitiş zamanı (UTC).</param>
public sealed record SynthesisJobDto(
    Guid Id,
    Guid BookId,
    string KitapBaslik,
    Guid VoiceProfileId,
    string SesProfiliAd,
    SentezIsDurumu Durum,
    int Yuzde,
    int ToplamBolum,
    int TamamlananBolum,
    int HataliBolum,
    Guid? MevcutBolumId,
    string? Mesaj,
    string MotorSirasi,
    string? KullanilanMotorlar,
    long GpuMs,
    decimal TahminiMaliyetUsd,
    double? TahminiSureSn,
    double UretilenSesSn,
    int OnbellektenParca,
    int UretilenParca,
    string? HataMesaji,
    DateTime OlusturmaTarihi,
    DateTime? BaslangicZamani,
    DateTime? BitisZamani);
