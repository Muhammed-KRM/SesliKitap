using SesliKitap.Data.Entities.Base;

namespace SesliKitap.Data.Entities;

/// <summary>
/// Kullanıcının bir kitabın belirli bir bölüm ve saniyesine eklediği yer imi (Faz 8).
/// </summary>
public sealed class Bookmark : AuditableEntity
{
    /// <summary>
    /// İlgili kitabın kimliği.
    /// </summary>
    public Guid BookId { get; set; }

    /// <summary>
    /// İlgili bölümün kimliği.
    /// </summary>
    public Guid ChapterId { get; set; }

    /// <summary>
    /// Bölüm içindeki saniye konumu.
    /// </summary>
    public double PozisyonSn { get; set; }

    /// <summary>
    /// Kullanıcının yer imine eklediği isteğe bağlı not (en fazla 1000 karakter).
    /// </summary>
    public string? Not { get; set; }

    /// <summary>
    /// İlgili kitap navigasyon özelliği.
    /// </summary>
    public Book? Book { get; set; }

    /// <summary>
    /// İlgili bölüm navigasyon özelliği.
    /// </summary>
    public BookChapter? Chapter { get; set; }
}
