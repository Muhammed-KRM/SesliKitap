namespace SesliKitap.Data.Entities.Base;

/// <summary>
/// Guid v7 kimlikli, oluşturma ve güncelleme zamanı otomatik tutulan taban varlık sınıfı.
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>
    /// Benzersiz varlık kimliği (UUID v7 — zamana göre sıralı).
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Kaydın oluşturulma zamanı (UTC).
    /// </summary>
    public DateTime OlusturmaTarihi { get; set; }

    /// <summary>
    /// Kaydın son güncellenme zamanı (UTC).
    /// </summary>
    public DateTime? GuncellemeTarihi { get; set; }
}
