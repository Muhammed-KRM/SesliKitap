namespace SesliKitap.Data.Entities;

/// <summary>
/// HTTP uç noktası (endpoint) istek ve yanıt bilgilerini saklayan log varlığı (Katman 1).
/// </summary>
public class EndpointLog
{
    /// <summary>Benzersiz log kimliği (Identity).</summary>
    public long Id { get; set; }

    /// <summary>İstek izleme kimliği (HttpContext.TraceIdentifier).</summary>
    public string? TraceId { get; set; }

    /// <summary>HTTP metodu (GET, POST, PUT, DELETE vb.).</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>İstek yolu (/api/...).</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Maskelenmiş sorgu dizesi (QueryString).</summary>
    public string? Query { get; set; }

    /// <summary>Maskelenmiş ve boyutu sınırlandırılmış istek gövdesi.</summary>
    public string? RequestBody { get; set; }

    /// <summary>Maskelenmiş ve boyutu sınırlandırılmış yanıt gövdesi.</summary>
    public string? ResponseBody { get; set; }

    /// <summary>HTTP durum kodu (200, 202, 400, 401, 500 vb.).</summary>
    public int StatusCode { get; set; }

    /// <summary>İstemci IP adresi.</summary>
    public string? IpAddress { get; set; }

    /// <summary>İstemci User-Agent başlığı.</summary>
    public string? UserAgent { get; set; }

    /// <summary>İsteğin toplam işlenme süresi (milisaniye).</summary>
    public long DurationMs { get; set; }

    /// <summary>Kaydın oluşturulma zamanı (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
