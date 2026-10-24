namespace SesliKitap.Business.DTOs.Bookmark;

/// <summary>
/// Kitap yer imi görüntüleme modeli (Faz 8).
/// </summary>
public sealed record BookmarkDto(
    Guid Id,
    Guid BookId,
    Guid ChapterId,
    int ChapterSiraNo,
    string ChapterBaslik,
    double PozisyonSn,
    string? Not,
    DateTime OlusturmaTarihi);

/// <summary>
/// Yeni yer imi oluşturma isteği.
/// </summary>
public sealed record BookmarkCreateRequest(
    Guid ChapterId,
    double PozisyonSn,
    string? Not = null);
