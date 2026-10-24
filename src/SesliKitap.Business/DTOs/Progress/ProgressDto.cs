namespace SesliKitap.Business.DTOs.Progress;

/// <summary>
/// Bir kitabın güncel dinleme konumunu, oynatma hızını ve son cihaz bilgisini temsil eden veri transfer nesnesi.
/// </summary>
/// <param name="BookId">Kitap kimliği.</param>
/// <param name="ChapterId">En son dinlenen bölüm kimliği.</param>
/// <param name="BolumSiraNo">En son dinlenen bölümün sıra numarası.</param>
/// <param name="PozisyonSn">Bölüm içindeki dinleme konumu (saniye).</param>
/// <param name="OynatmaHizi">Kitaba özel oynatma hızı (0.5 – 3.0).</param>
/// <param name="TamamlanmaYuzdesi">Kitap geneli süre bazlı tamamlanma yüzdesi (0 – 100).</param>
/// <param name="CihazKimligi">Son güncelleyen cihaz kimliği.</param>
/// <param name="CihazAdi">Son güncelleyen cihazın okunabilir adı.</param>
/// <param name="IstemciZamani">İstemcinin olay zaman damgası (UTC).</param>
/// <param name="TamamlandiMi">Kitabın tamamen bitirilip bitirilmediği.</param>
/// <param name="GuncellemeTarihi">Sunucu kayıt güncelleme tarihi (UTC).</param>
public sealed record ProgressDto(
    Guid BookId,
    Guid ChapterId,
    int BolumSiraNo,
    double PozisyonSn,
    double OynatmaHizi,
    double TamamlanmaYuzdesi,
    string CihazKimligi,
    string? CihazAdi,
    DateTime IstemciZamani,
    bool TamamlandiMi,
    DateTime GuncellemeTarihi);
