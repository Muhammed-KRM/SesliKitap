using System.ComponentModel.DataAnnotations;

namespace SesliKitap.Business.DTOs.Common;

/// <summary>
/// Sayfalı listeleme uç noktaları için ortak sayfa ve boyut sorgu modeli.
/// </summary>
public record PageQuery
{
    /// <summary>İstenen sayfa numarası (1 tabanlı). Varsayılan: 1.</summary>
    [Range(1, 100000)]
    public int Sayfa { get; init; } = 1;

    /// <summary>Sayfa başına kayıt sayısı. Varsayılan: 20, Azami: 100.</summary>
    [Range(1, 100)]
    public int Boyut { get; init; } = 20;

    /// <summary>Atlanacak kayıt sayısını (<c>(Sayfa - 1) * Boyut</c>) hesaplar.</summary>
    public int Skip => (Math.Max(1, Sayfa) - 1) * Math.Clamp(Boyut, 1, 100);

    /// <summary>Geçerli sayfa boyutunu (1–100 aralığında) döner.</summary>
    public int Take => Math.Clamp(Boyut, 1, 100);
}
