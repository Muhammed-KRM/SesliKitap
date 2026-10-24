using SesliKitap.Data.Enums;

namespace SesliKitap.Business.DTOs.Book;

/// <summary>
/// Keşif ekranından kütüphaneye kitap içe aktarma (<c>POST /api/books/import</c>) isteği.
/// </summary>
public sealed record BookImportRequest(
    KaynakTipi KaynakTipi,
    string KaynakKimligi,
    string IndirmeUrl,
    DosyaFormati DosyaFormati,
    string Baslik,
    string? Yazar = null,
    int? YayinYili = null,
    string? KapakUrl = null);
