namespace SesliKitap.Business.DTOs.Chapter;

/// <summary>
/// Ardışık bölümleri tek bölümde birleştirme isteği (<c>POST /api/books/{bookId}/chapters/merge</c>).
/// </summary>
/// <param name="ChapterIds">Birleştirilecek ardışık bölüm kimlikleri (en az 2).</param>
public sealed record ChapterMergeRequest(IReadOnlyList<Guid> ChapterIds);
