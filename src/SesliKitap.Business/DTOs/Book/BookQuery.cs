using SesliKitap.Data.Enums;

namespace SesliKitap.Business.DTOs.Book;

/// <summary>
/// Kütüphane kitap listesi filtreleme, sıralama ve sayfalama sorgu parametreleri.
/// </summary>
public sealed record BookQuery
{
    /// <summary>
    /// Başlık veya yazar içinde aranacak metin.
    /// </summary>
    public string? Arama { get; init; }

    /// <summary>
    /// Kitap durumu filtresi.
    /// </summary>
    public KitapDurumu? Durum { get; init; }

    /// <summary>
    /// Sıralama kriteri: <c>sonDinlenen</c>, <c>eklenme</c> (varsayılan) veya <c>baslik</c>.
    /// </summary>
    public string Siralama { get; init; } = "eklenme";

    /// <summary>
    /// 1 tabanlı sayfa numarası (varsayılan 1).
    /// </summary>
    public int Sayfa { get; init; } = 1;

    /// <summary>
    /// Sayfa başına kayıt sayısı (varsayılan 20, maksimum 100).
    /// </summary>
    public int Boyut { get; init; } = 20;
}
