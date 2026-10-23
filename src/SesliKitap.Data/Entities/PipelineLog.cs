namespace SesliKitap.Data.Entities;

/// <summary>
/// Kitap işleme ve seslendirme iş hattı (pipeline) aşamalarının başlangıç, ilerleme ve sonuç log varlığı (Katman 3).
/// </summary>
public class PipelineLog
{
    /// <summary>Benzersiz log kimliği (Identity).</summary>
    public long Id { get; set; }

    /// <summary>İlişkili kitap kimliği (varsa).</summary>
    public Guid? BookId { get; set; }

    /// <summary>İlişkili sentez işi kimliği (varsa).</summary>
    public Guid? JobId { get; set; }

    /// <summary>İlişkili kitap bölümü kimliği (varsa).</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>Pipeline aşaması adı (Indirme, MetinCikarma, Temizleme, Sentez vb.).</summary>
    public string Asama { get; set; } = string.Empty;

    /// <summary>Aşama durumu (Basladi, Devam, Tamamlandi, Hata, Uyari).</summary>
    public string Durum { get; set; } = "Basladi";

    /// <summary>İlerleme yüzdesi (0–100).</summary>
    public int? Yuzde { get; set; }

    /// <summary>İnsan tarafından okunabilir durum açıklaması.</summary>
    public string? Mesaj { get; set; }

    /// <summary>Aşama girdi meta verileri (JSONB).</summary>
    public string? GirdiMetadata { get; set; }

    /// <summary>Aşama çıktı meta verileri (JSONB).</summary>
    public string? CiktiMetadata { get; set; }

    /// <summary>Hata durumunda kısa hata mesajı.</summary>
    public string? HataMesaji { get; set; }

    /// <summary>Hata durumunda ayrıntılı yığın izi veya tanı bilgisi.</summary>
    public string? HataDetayi { get; set; }

    /// <summary>Logu üreten bileşen kaynağı (Api, Worker, Gpu).</summary>
    public string Kaynak { get; set; } = "Api";

    /// <summary>Uçtan uca izleme kimliği (TraceId).</summary>
    public string? TraceId { get; set; }

    /// <summary>Aşamanın başlangıç zamanı (UTC).</summary>
    public DateTime BaslangicZamani { get; set; } = DateTime.UtcNow;

    /// <summary>Aşamanın bitiş zamanı (UTC).</summary>
    public DateTime? BitisZamani { get; set; }

    /// <summary>Aşamanın toplam çalışma süresi (milisaniye).</summary>
    public long? SureMs { get; set; }

    /// <summary>Kaydın oluşturulma zamanı (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
