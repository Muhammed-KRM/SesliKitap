namespace SesliKitap.Business.DTOs.Chapter;

/// <summary>
/// Bir bölümü belirtilen karakter indeksinden (en yakın paragraf sınırına yuvarlayarak) ikiye bölme isteği (<c>POST /api/chapters/{id}/split</c>).
/// </summary>
/// <param name="KarakterIndeksi">Bölme noktasının karakter indeksi.</param>
/// <param name="YeniBaslik">Oluşacak ikinci bölümün başlığı (verilmezse otomatik türetilir).</param>
public sealed record ChapterSplitRequest(
    int KarakterIndeksi,
    string? YeniBaslik);
