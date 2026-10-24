using SesliKitap.Business.DTOs.Bookmark;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Kitap yer imlerini (bookmarks) yöneten iş servisi sözleşmesi (Faz 8).
/// </summary>
public interface IBookmarkService
{
    /// <summary>
    /// Belirtilen kitaba ait yer imlerini bölüm sırası ve saniye konumuna göre sıralı getirir.
    /// </summary>
    Task<IReadOnlyList<BookmarkDto>> GetByBookAsync(Guid bookId, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen kitaba yeni bir yer imi ekler.
    /// </summary>
    Task<BookmarkDto> CreateAsync(Guid bookId, BookmarkCreateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen yer imini siler.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
