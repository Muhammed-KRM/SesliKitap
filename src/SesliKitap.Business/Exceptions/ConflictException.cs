namespace SesliKitap.Business.Exceptions;

/// <summary>
/// Kaynak çakışması, iyimser kilit uyuşmazlığı veya tekrarlı kayıt hatasını temsil eden istisna sınıfı (HTTP 409 Conflict).
/// </summary>
public sealed class ConflictException : Exception
{
    /// <summary>Çakışmaya neden olan mevcut kaynağın kimliği (varsa).</summary>
    public Guid? ExistingId { get; }

    /// <summary>
    /// <see cref="ConflictException"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ConflictException()
    {
    }

    /// <summary>
    /// Belirtilen mesaj ve isteğe bağlı mevcut kaynak kimliğiyle <see cref="ConflictException"/> başlatır.
    /// </summary>
    /// <param name="message">Hata mesajı.</param>
    /// <param name="existingId">Mevcut kaynağın kimliği.</param>
    public ConflictException(string message, Guid? existingId = null) : base(message)
    {
        ExistingId = existingId;
    }

    /// <summary>
    /// Belirtilen mesaj ve iç istisnayla <see cref="ConflictException"/> başlatır.
    /// </summary>
    /// <param name="message">Hata mesajı.</param>
    /// <param name="innerException">İç istisna.</param>
    public ConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
