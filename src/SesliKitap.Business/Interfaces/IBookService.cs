using SesliKitap.Business.DTOs.Book;
using SesliKitap.Business.DTOs.Common;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Kitap yükleme, listeleme, güncelleme, silme, yeniden işleme ve dosya erişim işlemlerini yöneten iş servisi sözleşmesi.
/// </summary>
public interface IBookService
{
    /// <summary>
    /// Kütüphane kitap listesini filtreleme, sıralama ve sayfalama ile getirir.
    /// </summary>
    Task<PagedResult<BookListDto>> GetListAsync(BookQuery query, CancellationToken ct = default);

    /// <summary>
    /// "Dinlemeye devam et" rafı için son dinlenen kitapları getirir.
    /// </summary>
    Task<IReadOnlyList<BookListDto>> GetContinueListeningAsync(int count, CancellationToken ct = default);

    /// <summary>
    /// Kitap detayını bölüm özetleriyle birlikte getirir.
    /// </summary>
    Task<BookDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Yeni bir kitap dosyasını doğrular, MinIO'ya yükler, veritabanına kaydeder ve işleme olayını yayınlar.
    /// </summary>
    Task<BookDetailDto> UploadAsync(BookUploadRequest request, string traceId, CancellationToken ct = default);

    /// <summary>
    /// Kitap üst bilgilerini (başlık, yazar, açıklama, varsayılan ses profili) günceller.
    /// </summary>
    Task<BookDetailDto> UpdateAsync(Guid id, BookUpdateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Kitabı yumuşak siler (soft delete) ve önbelleği temizler.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Kitabın bölümlerini silip kaynak dosyadan metin çıkarma pipeline'ını yeniden başlatır.
    /// </summary>
    Task ReprocessAsync(Guid id, bool onay, string traceId, CancellationToken ct = default);

    /// <summary>
    /// Kitap kapağını MinIO'dan akış olarak açar.
    /// </summary>
    Task<(Stream Stream, string ContentType, string ETag)> OpenCoverAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Kitabın orijinal kaynak dosyasını MinIO'dan akış olarak açar.
    /// </summary>
    Task<(Stream Stream, string ContentType, string FileName)> OpenSourceAsync(Guid id, CancellationToken ct = default);
}
