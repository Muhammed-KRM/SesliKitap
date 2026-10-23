namespace SesliKitap.Business.Exceptions;

/// <summary>
/// İş kuralı ihlallerini temsil eden istisna sınıfı (HTTP 400 Bad Request).
/// </summary>
public class BusinessException : Exception
{
    /// <summary>İsteğe bağlı standart hata kodu.</summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// <see cref="BusinessException"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public BusinessException()
    {
    }

    /// <summary>
    /// Belirtilen hata mesajıyla <see cref="BusinessException"/> başlatır.
    /// </summary>
    /// <param name="message">Kullanıcıya gösterilebilir Türkçe hata mesajı.</param>
    /// <param name="errorCode">Standart hata kodu.</param>
    public BusinessException(string message, string? errorCode = null) : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Belirtilen hata mesajı ve iç istisna ile <see cref="BusinessException"/> başlatır.
    /// </summary>
    /// <param name="message">Kullanıcıya gösterilebilir Türkçe hata mesajı.</param>
    /// <param name="innerException">İç istisna.</param>
    public BusinessException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
