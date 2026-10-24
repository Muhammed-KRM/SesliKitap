using System.Text.Json;
using SesliKitap.Business.DTOs.Chapter;
using SesliKitap.Business.DTOs.Synthesis;
using SesliKitap.Data.Enums;

namespace SesliKitap.Business.DTOs.Book;

/// <summary>
/// Kitap detay ekranı için bölüm özetlerini (metinsiz) ve çıkarım istatistiklerini içeren kapsamlı görünüm modeli.
/// </summary>
/// <param name="Id">Kitap kimliği.</param>
/// <param name="Baslik">Kitap başlığı.</param>
/// <param name="Yazar">Kitap yazarı.</param>
/// <param name="Aciklama">Kitap açıklaması/özeti.</param>
/// <param name="Dil">Kitap dili (örn. <c>tr</c>).</param>
/// <param name="YayinYili">Yayın yılı.</param>
/// <param name="KaynakTipi">Kitabın kaynağı.</param>
/// <param name="DosyaFormati">Orijinal dosya formatı.</param>
/// <param name="DosyaBoyutu">Orijinal dosya boyutu (bayt).</param>
/// <param name="SayfaSayisi">Sayfa sayısı.</param>
/// <param name="KelimeSayisi">Dahil bölümlerin toplam kelime sayısı.</param>
/// <param name="ToplamSureSn">Hazır bölümlerin toplam ses süresi (saniye).</param>
/// <param name="Durum">Kitap durumu.</param>
/// <param name="HataMesaji">Son hata mesajı (varsa).</param>
/// <param name="VarsayilanSesProfiliId">Varsayılan ses profili kimliği.</param>
/// <param name="KapakVar">Kapak görseli olup olmadığı.</param>
/// <param name="Bolumler">Kitaba ait bölüm özetleri.</param>
/// <param name="Istatistik">Metin çıkarma ve temizlik istatistikleri JSON nesnesi.</param>
/// <param name="OlusturmaTarihi">Oluşturulma tarihi (UTC).</param>
/// <param name="SonDinlenmeTarihi">Son dinlenme tarihi (UTC).</param>
/// <param name="AktifIs">Kitap için devam eden aktif seslendirme işi (varsa).</param>
public sealed record BookDetailDto(
    Guid Id,
    string Baslik,
    string? Yazar,
    string? Aciklama,
    string Dil,
    int? YayinYili,
    KaynakTipi KaynakTipi,
    DosyaFormati DosyaFormati,
    long DosyaBoyutu,
    int? SayfaSayisi,
    int KelimeSayisi,
    double ToplamSureSn,
    KitapDurumu Durum,
    string? HataMesaji,
    Guid? VarsayilanSesProfiliId,
    bool KapakVar,
    IReadOnlyList<ChapterSummaryDto> Bolumler,
    JsonElement? Istatistik,
    DateTime OlusturmaTarihi,
    DateTime? SonDinlenmeTarihi,
    SynthesisJobDto? AktifIs = null);
