namespace SesliKitap.Business.Events;

/// <summary>
/// M4B dışa aktarımına dahil edilecek tekil bölüm bilgisi.
/// </summary>
public sealed record BookExportChapterItem(
    int SiraNo,
    string Baslik,
    string SesYolu,
    double SureSn);

/// <summary>
/// Bir kitabın hazır bölümlerinden tek parça M4B (Apple Books) sesli kitap dosyası üretilmesi için Python Worker'a gönderilen olay (Faz 8).
/// </summary>
public sealed record BookExportRequestedEvent : IntegrationEvent
{
    /// <summary>Dışa aktarılacak kitabın kimliği.</summary>
    public required Guid BookId { get; init; }

    /// <summary>Kitap başlığı.</summary>
    public required string Baslik { get; init; }

    /// <summary>Kitap yazarı.</summary>
    public string? Yazar { get; init; }

    /// <summary>Kitap kapağının MinIO anahtarı (varsa).</summary>
    public string? KapakYolu { get; init; }

    /// <summary>Üretilecek M4B dosyasının hedef MinIO anahtarı.</summary>
    public required string CiktiYolu { get; init; }

    /// <summary>Sıralı bölüm listesi.</summary>
    public required IReadOnlyList<BookExportChapterItem> Bolumler { get; init; }
}
