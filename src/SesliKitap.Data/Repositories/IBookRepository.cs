using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;

namespace SesliKitap.Data.Repositories;

/// <summary>
/// Kütüphane listesindeki tek bir kitap satırı için veritabanı projeksiyon modeli (metinsiz).
/// </summary>
/// <param name="Id">Kitap kimliği.</param>
/// <param name="Baslik">Kitap başlığı.</param>
/// <param name="Yazar">Kitap yazarı.</param>
/// <param name="Durum">Kitap işleme/seslendirme durumu.</param>
/// <param name="BolumSayisi">Seslendirmeye dahil bölüm sayısı.</param>
/// <param name="HazirBolumSayisi">Sesi hazır olan bölüm sayısı.</param>
/// <param name="ToplamSureSn">Hazır bölümlerin toplam ses süresi (saniye).</param>
/// <param name="TamamlanmaYuzdesi">Dinleme tamamlanma yüzdesi.</param>
/// <param name="KapakVar">Kapak görseli olup olmadığı.</param>
/// <param name="OlusturmaTarihi">Oluşturulma tarihi (UTC).</param>
/// <param name="SonDinlenmeTarihi">Son dinlenme tarihi (UTC).</param>
public sealed record BookListRow(
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

/// <summary>
/// Kitap varlığı için özelleştirilmiş veri erişim sözleşmesi.
/// </summary>
public interface IBookRepository : IRepository<Book>
{
    /// <summary>
    /// Verilen SHA-256 özetine sahip (silinmemiş) kitabı bulur.
    /// </summary>
    /// <param name="hash">64 karakterlik SHA-256 dosya özeti.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Bulunursa kitap, aksi halde <c>null</c>.</returns>
    Task<Book?> FindByHashAsync(string hash, CancellationToken ct = default);

    /// <summary>
    /// Filtreleme, sıralama ve sayfalama kriterlerine göre kitap listesini projeksiyonla getirir.
    /// </summary>
    /// <param name="arama">Başlık veya yazar arama metni.</param>
    /// <param name="durum">Kitap durumu filtresi.</param>
    /// <param name="siralama">Sıralama alanı (<c>sonDinlenen</c>, <c>eklenme</c>, <c>baslik</c>).</param>
    /// <param name="sayfa">1 tabanlı sayfa numarası.</param>
    /// <param name="boyut">Sayfa başına kayıt sayısı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Sayfalanmış kitap satırları ve toplam kayıt sayısı.</returns>
    Task<(IReadOnlyList<BookListRow> Items, int Toplam)> GetPagedListAsync(
        string? arama,
        KitapDurumu? durum,
        string? siralama,
        int sayfa,
        int boyut,
        CancellationToken ct = default);

    /// <summary>
    /// "Dinlemeye devam et" rafı için son dinlenen kitapları getirir.
    /// </summary>
    /// <param name="count">Maksimum kitap sayısı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Son dinlenen kitap listesi.</returns>
    Task<IReadOnlyList<BookListRow>> GetContinueListeningAsync(int count, CancellationToken ct = default);

    /// <summary>
    /// Kitabın durumunu ve hata mesajını günceller.
    /// </summary>
    /// <param name="id">Kitap kimliği.</param>
    /// <param name="durum">Yeni kitap durumu.</param>
    /// <param name="hataMesaji">Varsa hata mesajı.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task UpdateStatusAsync(Guid id, KitapDurumu durum, string? hataMesaji = null, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen tarihten önce yumuşak silinmiş kitapları (filtreleri yoksayarak) getirir.
    /// </summary>
    /// <param name="cutoffUtc">Eşik zamanı (UTC).</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Kalıcı silinmeye aday kitaplar.</returns>
    Task<IReadOnlyList<Book>> GetExpiredSoftDeletedBooksAsync(DateTime cutoffUtc, CancellationToken ct = default);

    /// <summary>
    /// Kitabı veritabanından fiziksel olarak siler.
    /// </summary>
    /// <param name="id">Kitap kimliği.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task HardDeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Verilen işlemi veritabanı yürütme stratejisi ve transaction içinde çalıştırır.
    /// </summary>
    /// <param name="action">Çalıştırılacak asenkron işlem.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
