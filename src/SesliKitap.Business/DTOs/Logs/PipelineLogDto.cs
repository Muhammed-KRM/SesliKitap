namespace SesliKitap.Business.DTOs.Logs;

/// <summary>
/// Pipeline aşaması log bağlam bilgisi.
/// </summary>
/// <param name="BookId">İlişkili kitap kimliği.</param>
/// <param name="JobId">İlişkili sentez işi kimliği.</param>
/// <param name="ChapterId">İlişkili bölüm kimliği.</param>
/// <param name="TraceId">Dağıtık izleme kimliği.</param>
/// <param name="Kaynak">Log kaynağı bileşeni.</param>
public sealed record PipelineLogContext(
    Guid? BookId = null,
    Guid? JobId = null,
    Guid? ChapterId = null,
    string? TraceId = null,
    string? Kaynak = "Api");

/// <summary>
/// Pipeline olayı veya aşama log kaydı isteği.
/// </summary>
public sealed class PipelineLogRequest
{
    /// <summary>Kitap kimliği.</summary>
    public Guid? BookId { get; set; }

    /// <summary>Sentez işi kimliği.</summary>
    public Guid? JobId { get; set; }

    /// <summary>Bölüm kimliği.</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>Aşama adı.</summary>
    public string Asama { get; set; } = string.Empty;

    /// <summary>Durum (Basladi, Devam, Tamamlandi, Hata).</summary>
    public string Durum { get; set; } = string.Empty;

    /// <summary>Tamamlanma yüzdesi (0-100).</summary>
    public int? Yuzde { get; set; }

    /// <summary>Log mesajı.</summary>
    public string? Mesaj { get; set; }

    /// <summary>Girdi meta verisi.</summary>
    public string? GirdiMetadata { get; set; }

    /// <summary>Çıktı meta verisi.</summary>
    public string? CiktiMetadata { get; set; }

    /// <summary>Hata mesajı.</summary>
    public string? HataMesaji { get; set; }

    /// <summary>Hata detayı.</summary>
    public string? HataDetayi { get; set; }

    /// <summary>Kaynak bileşen.</summary>
    public string? Kaynak { get; set; }

    /// <summary>İzleme kimliği.</summary>
    public string? TraceId { get; set; }

    /// <summary>Aşama süresi (milisaniye).</summary>
    public long? SureMs { get; set; }
}
