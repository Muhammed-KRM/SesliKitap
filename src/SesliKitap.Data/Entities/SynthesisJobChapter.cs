using SesliKitap.Data.Enums;

namespace SesliKitap.Data.Entities;

/// <summary>
/// Bir sentez işine dahil edilen tekil bölümün iş içi durumunu temsil eden varlık (<c>synthesis_job_chapters</c>, Bölüm 4.6.2).
/// </summary>
public sealed class SynthesisJobChapter
{
    /// <summary>Sentez işi kimliği (PK parçası, FK).</summary>
    public Guid JobId { get; set; }

    /// <summary>Sentez işi navigasyon özelliği.</summary>
    public SynthesisJob? Job { get; set; }

    /// <summary>Kitap bölümü kimliği (PK parçası, FK).</summary>
    public Guid ChapterId { get; set; }

    /// <summary>Kitap bölümü navigasyon özelliği.</summary>
    public BookChapter? Chapter { get; set; }

    /// <summary>İş içindeki işlenme sırası (1'den başlar).</summary>
    public int Sira { get; set; }

    /// <summary>Bu bölümün iş kapsamındaki seslendirme durumu.</summary>
    public BolumSesDurumu Durum { get; set; } = BolumSesDurumu.Kuyrukta;

    /// <summary>Bu bölüm için harcanan GPU süresi (ms).</summary>
    public long GpuMs { get; set; }

    /// <summary>Varsa bölüme özgü hata mesajı.</summary>
    public string? HataMesaji { get; set; }
}
