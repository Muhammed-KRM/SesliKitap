namespace SesliKitap.Business.Exceptions;

/// <summary>
/// İstenen varlığın bulunamadığını temsil eden istisna sınıfı (HTTP 404 Not Found).
/// </summary>
public sealed class NotFoundException : Exception
{
    /// <summary>
    /// <see cref="NotFoundException"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public NotFoundException()
    {
    }

    /// <summary>
    /// Belirtilen mesajla <see cref="NotFoundException"/> başlatır.
    /// </summary>
    /// <param name="message">Hata mesajı.</param>
    public NotFoundException(string message) : base(message)
    {
    }

    /// <summary>
    /// Varlık adı ve anahtar değeriyle <see cref="NotFoundException"/> başlatır.
    /// </summary>
    /// <param name="entity">Varlık adı (örn. "Kitap").</param>
    /// <param name="key">Aranan anahtar değeri.</param>
    public NotFoundException(string entity, object key) : base($"{entity} bulunamadı (Id: {key}).")
    {
    }

    /// <summary>
    /// Belirtilen mesaj ve iç istisnayla <see cref="NotFoundException"/> başlatır.
    /// </summary>
    /// <param name="message">Hata mesajı.</param>
    /// <param name="innerException">İç istisna.</param>
    public NotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
