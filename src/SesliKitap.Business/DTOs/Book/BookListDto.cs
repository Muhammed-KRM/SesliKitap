using SesliKitap.Data.Enums;

namespace SesliKitap.Business.DTOs.Book;

/// <summary>
/// Kütüphane listesindeki ve "Dinlemeye devam et" rafındaki bir kitabın özet görünüm modeli.
/// </summary>
/// <param name="Id">Kitap kimliği.</param>
/// <param name="Baslik">Kitap başlığı.</param>
/// <param name="Yazar">Kitap yazarı.</param>
/// <param name="Durum">Kitap durumu.</param>
/// <param name="BolumSayisi">Seslendirmeye dahil bölüm sayısı.</param>
/// <param name="HazirBolumSayisi">Sesi hazır olan bölüm sayısı.</param>
/// <param name="ToplamSureSn">Hazır bölümlerin toplam ses süresi (saniye).</param>
/// <param name="TamamlanmaYuzdesi">Dinleme tamamlanma yüzdesi.</param>
/// <param name="KapakVar">Kapak görseli olup olmadığı.</param>
/// <param name="OlusturmaTarihi">Oluşturulma tarihi (UTC).</param>
/// <param name="SonDinlenmeTarihi">Son dinlenme tarihi (UTC).</param>
public sealed record BookListDto(
    Guid Id,
    string Baslik,
    string? Yazar,
    KitapDurumu Durum,
    int BolumSayisi,
    int HazirBolumSayisi,
    double ToplamSureSn,
    double? TamamlanmaYuzdesi,
    bool KapakVar,
    DateTime OlusturmaTarihi,
    DateTime? SonDinlenmeTarihi);
