namespace SesliKitap.Business.DTOs.Chapter;

/// <summary>
/// Kitaptaki bölümlerin sırasını yeniden düzenleme isteği (<c>PUT /api/books/{bookId}/chapters/order</c>).
/// </summary>
/// <param name="ChapterIds">Yeni sırayla tüm bölüm kimlikleri.</param>
public sealed record ChapterOrderRequest(IReadOnlyList<Guid> ChapterIds);
