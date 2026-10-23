using SesliKitap.Business.DTOs.Chapter;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Bölüm okuma, metin düzenleme, birleştirme, bölme, sıralama ve dahil/hariç yönetimi iş servisi sözleşmesi.
/// </summary>
public interface IChapterService
{
    /// <summary>
    /// Kitaba ait bölümlerin özet listesini (metinsiz) getirir.
    /// </summary>
    Task<IReadOnlyList<ChapterSummaryDto>> GetSummariesByBookAsync(Guid bookId, CancellationToken ct = default);

    /// <summary>
    /// Bölüm detayını temiz metniyle birlikte getirir.
    /// </summary>
    Task<ChapterDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Bölüm başlığını, metnini ve dahil olma durumunu iyimser kilitleme (<c>MetinSurumu</c>) kontrolüyle günceller.
    /// </summary>
    Task<ChapterDetailDto> UpdateAsync(Guid id, ChapterUpdateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Ardışık sıra numaralı bölümleri tek bir bölümde birleştirir.
    /// </summary>
    Task<ChapterDetailDto> MergeAsync(Guid bookId, ChapterMergeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Bir bölümü belirtilen karakter indeksine en yakın paragraf sınırından ikiye böler.
    /// </summary>
    Task<IReadOnlyList<ChapterSummaryDto>> SplitAsync(Guid id, ChapterSplitRequest request, CancellationToken ct = default);

    /// <summary>
    /// Kitaptaki bölümlerin sıra numaralarını yeniden düzenler.
    /// </summary>
    Task<IReadOnlyList<ChapterSummaryDto>> ReorderAsync(Guid bookId, ChapterOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen bölümlerin seslendirmeye dahil olma durumunu toplu olarak günceller.
    /// </summary>
    Task<IReadOnlyList<ChapterSummaryDto>> SetInclusionAsync(Guid bookId, ChapterInclusionRequest request, CancellationToken ct = default);
}
