namespace SesliKitap.Business.Events;

/// <summary>
/// Sentez işi tamamlandığında, kısmen tamamlandığında veya iptal edildiğinde Python Worker tarafından yayınlanan olay (Bölüm 4.7.2).
/// </summary>
public sealed record SynthesisCompletedEvent : IntegrationEvent
{
    /// <summary>Sentez işi kimliği.</summary>
    public required Guid JobId { get; init; }

    /// <summary>Kitap kimliği.</summary>
    public required Guid BookId { get; init; }

    /// <summary>İşin nihai sonuç durumu (<c>Tamamlandi</c>, <c>KismenTamamlandi</c>, <c>IptalEdildi</c>, <c>Hata</c>).</summary>
    public required string Sonuc { get; init; }

    /// <summary>Başarıyla tamamlanan bölüm sayısı.</summary>
    public required int TamamlananBolum { get; init; }

    /// <summary>Hata alan bölüm sayısı.</summary>
    public required int HataliBolum { get; init; }

    /// <summary>İş boyunca harcanan toplam GPU çıkarım süresi (ms).</summary>
    public required long ToplamGpuMs { get; init; }

    /// <summary>İş boyunca üretilen toplam ses süresi (sn).</summary>
    public required double UretilenSesSn { get; init; }

    /// <summary>İşin kullanıcı iptaliyle mi durduğu.</summary>
    public required bool IptalEdildi { get; init; }

    /// <summary>İş boyunca fiilen kullanılan motor kimlikleri (virgülle ayrılmış veya liste).</summary>
    public string? KullanilanMotorlar { get; init; }
}
