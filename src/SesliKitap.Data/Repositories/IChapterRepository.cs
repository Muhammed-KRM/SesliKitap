using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;

namespace SesliKitap.Data.Repositories;

/// <summary>
/// Bölüm listesi için metinsiz veritabanı projeksiyon modeli.
/// </summary>
/// <param name="Id">Bölüm kimliği.</param>
/// <param name="BookId">Kitap kimliği.</param>
/// <param name="SiraNo">Sıra numarası (1 tabanlı).</param>
/// <param name="Baslik">Bölüm başlığı.</param>
/// <param name="KelimeSayisi">Kelime sayısı.</param>
/// <param name="KarakterSayisi">Karakter sayısı.</param>
/// <param name="DahilMi">Seslendirmeye dahil mi.</param>
/// <param name="OtomatikHaricNedeni">Otomatik hariç tutma nedeni (varsa).</param>
/// <param name="SesDurumu">Bölüm ses durumu.</param>
/// <param name="SureSn">Üretilmiş ses süresi (saniye).</param>
/// <param name="MetinSurumu">Metin sürüm numarası.</param>
public sealed record ChapterSummaryRow(
    Guid Id,
    Guid BookId,
    int SiraNo,
    string Baslik,
    int KelimeSayisi,
    int KarakterSayisi,
    bool DahilMi,
    string? OtomatikHaricNedeni,
    BolumSesDurumu SesDurumu,
    double? SureSn,
    int MetinSurumu);

/// <summary>
/// Kitap bölümleri için özelleştirilmiş veri erişim sözleşmesi.
/// </summary>
public interface IChapterRepository : IRepository<BookChapter>
{
    /// <summary>
    /// Belirtilen kitaba ait bölümlerin özetlerini (<c>TemizMetin</c> kolonunu çekmeden) sıra numarasıyla getirir.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Sıralı bölüm özetleri.</returns>
    Task<IReadOnlyList<ChapterSummaryRow>> GetSummariesAsync(Guid bookId, CancellationToken ct = default);

    /// <summary>
    /// Bölüm detayını temiz metniyle birlikte getirir.
    /// </summary>
    /// <param name="id">Bölüm kimliği.</param>
    /// <param name="asNoTracking">Değişiklik izlemenin kapatılıp kapatılmayacağı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Bulunursa bölüm, aksi halde <c>null</c>.</returns>
    Task<BookChapter?> GetWithTextAsync(Guid id, bool asNoTracking = true, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen kitaba ait tüm bölümleri sıra numarasına göre getirir.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <param name="asNoTracking">Değişiklik izlemenin kapatılıp kapatılmayacağı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Sıralı bölüm varlıkları.</returns>
    Task<IReadOnlyList<BookChapter>> GetByBookIdOrderedAsync(Guid bookId, bool asNoTracking = false, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen kitaba ait tüm bölümleri fiziksel olarak siler (yeniden işleme için).
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task DeleteByBookIdAsync(Guid bookId, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen sıra numarasından itibaren bölümlerin sıra numarasını iki aşamalı (negatif geçici sıra numarasıyla) kaydırır.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <param name="fromSiraNo">Kaydırmanın başlayacağı sıra numarası (dahil).</param>
    /// <param name="delta">Eklenecek/çıkarılacak sıra farkı (+1, -1 vb.).</param>
    /// <param name="ct">İptal belirteci.</param>
    Task ShiftOrderAsync(Guid bookId, int fromSiraNo, int delta, CancellationToken ct = default);

    /// <summary>
    /// Kitabın seslendirmeye dahil bölümlerinin toplam kelime sayısını yeniden hesaplayıp <c>books.kelime_sayisi</c> alanını günceller.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task<int> RecalculateBookWordCountAsync(Guid bookId, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen bölümlerin seslendirmeye dahil olma durumunu toplu olarak günceller.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <param name="chapterIds">Bölüm kimlikleri.</param>
    /// <param name="dahilMi">Yeni dahil olma durumu.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task UpdateInclusionBulkAsync(Guid bookId, IReadOnlyCollection<Guid> chapterIds, bool dahilMi, CancellationToken ct = default);

    /// <summary>
    /// Sentez işi başlatılırken veya maliyet tahmini yapılırken hedef bölümleri temiz metinleriyle sıra numarasına göre getirir.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <param name="chapterIds">Belirli bölüm kimlikleri (boşsa <c>DahilMi=true</c> olan tüm bölümler).</param>
    /// <param name="onlyMissing">Yalnızca sesi hazır olmayan veya güncel olmayan bölümlerin mi seçileceği.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task<IReadOnlyList<BookChapter>> GetForSynthesisAsync(
        Guid bookId,
        IReadOnlyList<Guid>? chapterIds,
        bool onlyMissing,
        CancellationToken ct = default);

    /// <summary>
    /// Kitabın sesi hazır olan bölümlerinin toplam ses süresini yeniden hesaplayıp <c>books.toplam_sure_sn</c> alanını günceller.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task<double> RecalculateBookAudioDurationAsync(Guid bookId, CancellationToken ct = default);
}
