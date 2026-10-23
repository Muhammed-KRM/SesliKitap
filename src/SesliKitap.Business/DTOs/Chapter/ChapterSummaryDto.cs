using SesliKitap.Data.Enums;

namespace SesliKitap.Business.DTOs.Chapter;

/// <summary>
/// Kitap detayında ve bölüm listesinde kullanılan metinsiz bölüm özet modeli.
/// </summary>
/// <param name="Id">Bölüm kimliği.</param>
/// <param name="SiraNo">1 tabanlı sıra numarası.</param>
/// <param name="Baslik">Bölüm başlığı.</param>
/// <param name="KelimeSayisi">Kelime sayısı.</param>
/// <param name="KarakterSayisi">Karakter sayısı.</param>
/// <param name="DahilMi">Seslendirmeye dahil mi.</param>
/// <param name="OtomatikHaricNedeni">Otomatik hariç tutma nedeni (varsa).</param>
/// <param name="SesDurumu">Bölüm ses durumu.</param>
/// <param name="SureSn">Üretilen gerçek ses süresi (saniye).</param>
/// <param name="TahminiSureSn">Kelime sayısından hesaplanan tahmini ses süresi (saniye).</param>
/// <param name="MetinSurumu">Metin sürüm numarası (iyimser kilitleme için).</param>
public sealed record ChapterSummaryDto(
    Guid Id,
    int SiraNo,
    string Baslik,
    int KelimeSayisi,
    int KarakterSayisi,
    bool DahilMi,
    string? OtomatikHaricNedeni,
    BolumSesDurumu SesDurumu,
    double? SureSn,
    double TahminiSureSn,
    int MetinSurumu);
