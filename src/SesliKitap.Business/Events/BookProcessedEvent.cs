namespace SesliKitap.Business.Events;

/// <summary>
/// Worker tarafından kaynak dosyadan çıkarılan kitap meta verisi.
/// </summary>
/// <param name="Baslik">Dosya meta verisinden çıkarılan başlık.</param>
/// <param name="Yazar">Dosya meta verisinden çıkarılan yazar.</param>
/// <param name="Dil">Tespit edilen dil kodu (örn. <c>tr</c>).</param>
/// <param name="YayinYili">Tespit edilen yayın yılı.</param>
/// <param name="SayfaSayisi">Toplam sayfa sayısı.</param>
public sealed record ProcessedBookMeta(
    string? Baslik,
    string? Yazar,
    string? Dil,
    int? YayinYili,
    int? SayfaSayisi);

/// <summary>
/// Worker tarafından gerçekleştirilen metin temizleme ve bölüm tespiti istatistikleri.
/// </summary>
/// <param name="SilinenUstBilgi">Silinen tekrar eden üst/alt bilgi satırı sayısı.</param>
/// <param name="SilinenSayfaNo">Silinen sayfa numarası satırı sayısı.</param>
/// <param name="SilinenDipnot">Silinen dipnot bloğu/işareti sayısı.</param>
/// <param name="BirlesenTire">Satır sonunda birleştirilen tireli kelime sayısı.</param>
/// <param name="BolumTespitYontemi">Kullanılan bölüm tespit stratejisi (<c>epub_toc</c>, <c>pdf_outline</c>, <c>heading_heuristic</c>, <c>fixed_size</c>).</param>
public sealed record ProcessedBookStats(
    int SilinenUstBilgi,
    int SilinenSayfaNo,
    int SilinenDipnot,
    int BirlesenTire,
    string BolumTespitYontemi);

/// <summary>
/// MinIO'daki <c>chapters.json</c> içindeki tek bir bölüm öğesi.
/// </summary>
/// <param name="SiraNo">1 tabanlı bölüm sıra numarası.</param>
/// <param name="Baslik">Bölüm başlığı.</param>
/// <param name="TemizMetin">Temizlenmiş bölüm metni.</param>
/// <param name="KelimeSayisi">Kelime sayısı.</param>
/// <param name="KarakterSayisi">Karakter sayısı.</param>
/// <param name="DahilMi">Seslendirmeye dahil mi.</param>
/// <param name="OtomatikHaricNedeni">Otomatik hariç tutulduysa nedeni.</param>
public sealed record ProcessedChapterItem(
    int SiraNo,
    string Baslik,
    string TemizMetin,
    int KelimeSayisi,
    int KarakterSayisi,
    bool DahilMi,
    string? OtomatikHaricNedeni);

/// <summary>
/// MinIO'ya yazılan <c>books/{bookId}/processed/chapters.json</c> dosyasının kök şeması.
/// </summary>
/// <param name="BookId">Kitap kimliği.</param>
/// <param name="Chapters">Çıkarılan bölümler listesi.</param>
public sealed record ProcessedChaptersPayload(
    Guid BookId,
    IReadOnlyList<ProcessedChapterItem> Chapters);

/// <summary>
/// Python Worker'ın kitap metin çıkarma, temizleme ve bölümleme işlemini tamamladığında yayınladığı olay.
/// </summary>
public sealed record BookProcessedEvent : IntegrationEvent
{
    /// <summary>
    /// İşlenen kitabın kimliği.
    /// </summary>
    public required Guid BookId { get; init; }

    /// <summary>
    /// Çıkarılan bölümlerin bulunduğu MinIO JSON nesne anahtarı (<c>books/{id}/processed/chapters.json</c>).
    /// </summary>
    public string? BolumlerYolu { get; init; }

    /// <summary>
    /// Üretilen kapak görselinin MinIO nesne anahtarı (<c>books/{id}/cover.jpg</c>).
    /// </summary>
    public string? KapakYolu { get; init; }

    /// <summary>
    /// Çıkarılan kitap meta verisi.
    /// </summary>
    public ProcessedBookMeta? Meta { get; init; }

    /// <summary>
    /// Temizlik ve bölümleme istatistikleri.
    /// </summary>
    public ProcessedBookStats? Istatistik { get; init; }

    /// <summary>
    /// Kitabın taranmış (metin katmansız görüntü) PDF olup OCR gerektirip gerektirmediği.
    /// </summary>
    public bool OcrGerekli { get; init; }
}
