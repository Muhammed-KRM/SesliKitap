namespace SesliKitap.Business.DTOs.Common;

/// <summary>
/// Sayfalı listeleme yanıtlarını temsil eden değişmez veri transfer nesnesi.
/// </summary>
/// <typeparam name="T">Liste öğesi tipi.</typeparam>
/// <param name="Kayitlar">Mevcut sayfadaki öğe listesi.</param>
/// <param name="ToplamKayit">Filtreye uyan toplam kayıt sayısı.</param>
/// <param name="Sayfa">Mevcut sayfa numarası (1 tabanlı).</param>
/// <param name="Boyut">Sayfa başına kayıt sayısı.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Kayitlar,
    int ToplamKayit,
    int Sayfa,
    int Boyut)
{
    /// <summary>Toplam sayfa sayısını hesaplar.</summary>
    public int ToplamSayfa => Boyut <= 0 ? 0 : (int)Math.Ceiling((double)ToplamKayit / Boyut);
}
