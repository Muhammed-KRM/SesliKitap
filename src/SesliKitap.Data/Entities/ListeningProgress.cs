using SesliKitap.Data.Entities.Base;

namespace SesliKitap.Data.Entities;

/// <summary>
/// Bir kitabın son dinleme konumunu, oynatma hızını ve cihaz bilgisini tutan varlık.
/// </summary>
public sealed class ListeningProgress : AuditableEntity
{
    /// <summary>
    /// İlgili kitabın kimliği (kitap başına tek kayıt, benzersiz).
    /// </summary>
    public Guid BookId { get; set; }

    /// <summary>
    /// En son dinlenen bölümün kimliği.
    /// </summary>
    public Guid ChapterId { get; set; }

    /// <summary>
    /// Bölüm içindeki dinleme konumu (saniye).
    /// </summary>
    public double PozisyonSn { get; set; }

    /// <summary>
    /// Kitaba özel oynatma hızı (0.5 – 3.0).
    /// </summary>
    public double OynatmaHizi { get; set; } = 1.0;

    /// <summary>
    /// Kitap genelinde süre bazlı tamamlanma yüzdesi (0 – 100).
    /// </summary>
    public double TamamlanmaYuzdesi { get; set; }

    /// <summary>
    /// Son güncelleyen istemci cihazın benzersiz kimliği.
    /// </summary>
    public string CihazKimligi { get; set; } = string.Empty;

    /// <summary>
    /// Son güncelleyen istemci cihazın okunabilir adı (ör. "Windows · Chrome").
    /// </summary>
    public string? CihazAdi { get; set; }

    /// <summary>
    /// İstemcinin konum olayını ürettiği UTC zaman damgası (son-yazan-kazanır karşılaştırması için).
    /// </summary>
    public DateTime IstemciZamani { get; set; }

    /// <summary>
    /// Kitabın tamamen bitirilip bitirilmediği.
    /// </summary>
    public bool TamamlandiMi { get; set; }

    /// <summary>
    /// PostgreSQL iyimser eşzamanlılık belirteci (<c>xmin</c>).
    /// </summary>
    public uint Xmin { get; set; }

    /// <summary>
    /// İlgili kitap navigasyon özelliği.
    /// </summary>
    public Book? Book { get; set; }

    /// <summary>
    /// İlgili bölüm navigasyon özelliği.
    /// </summary>
    public BookChapter? Chapter { get; set; }
}
