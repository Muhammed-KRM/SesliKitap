namespace SesliKitap.Business.DTOs.Chapter;

/// <summary>
/// Bölüm başlığı, metni ve dahil olma durumunu güncelleme isteği (<c>PUT /api/chapters/{id}</c>).
/// </summary>
/// <param name="Baslik">Yeni bölüm başlığı.</param>
/// <param name="TemizMetin">Düzenlenmiş temiz metin.</param>
/// <param name="DahilMi">Seslendirmeye dahil mi.</param>
/// <param name="MetinSurumu">İstemcinin elindeki metin sürüm numarası (uyuşmazsa 409 Conflict döner).</param>
public sealed record ChapterUpdateRequest(
    string Baslik,
    string TemizMetin,
    bool DahilMi,
    int MetinSurumu);
