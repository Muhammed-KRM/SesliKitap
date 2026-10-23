namespace SesliKitap.Business.Exceptions;

/// <summary>
/// Kitap işleme veya seslendirme iş hattında oluşan hataları temsil eden istisna sınıfı (HTTP 500 Internal Server Error).
/// </summary>
public sealed class PipelineException : Exception
{
    /// <summary>
    /// <see cref="PipelineException"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public PipelineException()
    {
    }

    /// <summary>
    /// Belirtilen mesajla <see cref="PipelineException"/> başlatır.
    /// </summary>
    /// <param name="message">Hata mesajı.</param>
    public PipelineException(string message) : base(message)
    {
    }

    /// <summary>
    /// Belirtilen mesaj ve iç istisnayla <see cref="PipelineException"/> başlatır.
    /// </summary>
    /// <param name="message">Hata mesajı.</param>
    /// <param name="innerException">İç istisna.</param>
    public PipelineException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}
