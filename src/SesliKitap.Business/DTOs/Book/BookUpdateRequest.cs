namespace SesliKitap.Business.DTOs.Book;

/// <summary>
/// Kitap üst bilgilerini güncelleme isteği (<c>PUT /api/books/{id}</c>).
/// </summary>
/// <param name="Baslik">Yeni kitap başlığı.</param>
/// <param name="Yazar">Yeni yazar adı.</param>
/// <param name="Aciklama">Kitap açıklaması/özeti.</param>
/// <param name="VarsayilanSesProfiliId">Varsayılan ses profili kimliği.</param>
public sealed record BookUpdateRequest(
    string Baslik,
    string? Yazar,
    string? Aciklama,
    Guid? VarsayilanSesProfiliId);
