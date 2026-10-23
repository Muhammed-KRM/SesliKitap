namespace SesliKitap.Data.Entities;

/// <summary>
/// Servis ve fonksiyon seviyesindeki istisna ve uyarı kayıtlarını saklayan log varlığı (Katman 2).
/// </summary>
public class FunctionLog
{
    /// <summary>Benzersiz log kimliği (Identity).</summary>
    public long Id { get; set; }

    /// <summary>Standart hata veya uyarı kodu (örn. BOOK_UPLOAD_ERR).</summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>Hatayı üreten sınıf adı.</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Hatayı üreten metot adı ([CallerMemberName]).</summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>Kaynak dosya yolu ([CallerFilePath]).</summary>
    public string? FilePath { get; set; }

    /// <summary>Kaynak dosyadaki satır numarası ([CallerLineNumber]).</summary>
    public int LineNumber { get; set; }

    /// <summary>İstisna veya uyarı mesajı.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>İstisna yığın izi (StackTrace).</summary>
    public string? StackTrace { get; set; }

    /// <summary>Metoda geçilen girdi nesnesinin tipi.</summary>
    public string? InputType { get; set; }

    /// <summary>Maskelenmiş girdi nesnesi JSON temsili.</summary>
    public string? InputValue { get; set; }

    /// <summary>İlişkili istek veya olay izleme kimliği.</summary>
    public string? TraceId { get; set; }

    /// <summary>Kayıt önem derecesi (Error, Warning, Critical).</summary>
    public string Severity { get; set; } = "Error";

    /// <summary>Kaydın oluşturulma zamanı (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
