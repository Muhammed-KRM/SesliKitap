namespace SesliKitap.Business.Exceptions;

/// <summary>
/// Harici bir altyapı veya servis (MinIO, RabbitMQ, TTS Sunucusu vb.) hatasını temsil eden istisna sınıfı (HTTP 502 Bad Gateway).
/// </summary>
public sealed class ExternalServiceException : Exception
{
    /// <summary>Hatanın kaynaklandığı harici servis adı.</summary>
    public string ServiceName { get; } = string.Empty;

    /// <summary>
    /// <see cref="ExternalServiceException"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ExternalServiceException()
    {
    }

    /// <summary>
    /// Belirtilen mesajla <see cref="ExternalServiceException"/> başlatır.
    /// </summary>
    /// <param name="message">Hata mesajı.</param>
    public ExternalServiceException(string message) : base(message)
    {
    }

    /// <summary>
    /// Belirtilen mesaj ve iç istisnayla <see cref="ExternalServiceException"/> başlatır.
    /// </summary>
    /// <param name="message">Hata mesajı.</param>
    /// <param name="innerException">İç istisna.</param>
    public ExternalServiceException(string message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Servis adı, hata mesajı ve isteğe bağlı iç istisnayla <see cref="ExternalServiceException"/> başlatır.
    /// </summary>
    /// <param name="serviceName">Harici servis adı (örn. "MinIO").</param>
    /// <param name="message">Hata açıklaması.</param>
    /// <param name="innerException">İç istisna.</param>
    public ExternalServiceException(string serviceName, string message, Exception? innerException = null)
        : base($"{serviceName}: {message}", innerException)
    {
        ServiceName = serviceName;
    }
}
