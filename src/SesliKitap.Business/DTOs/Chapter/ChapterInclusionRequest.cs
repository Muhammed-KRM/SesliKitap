namespace SesliKitap.Business.DTOs.Chapter;

/// <summary>
/// Birden çok bölümün seslendirmeye dahil olma durumunu toplu güncelleme isteği (<c>PATCH /api/books/{bookId}/chapters/inclusion</c>).
/// </summary>
/// <param name="ChapterIds">Güncellenecek bölüm kimlikleri.</param>
/// <param name="DahilMi">Yeni dahil olma durumu.</param>
public sealed record ChapterInclusionRequest(
    IReadOnlyList<Guid> ChapterIds,
    bool DahilMi);
