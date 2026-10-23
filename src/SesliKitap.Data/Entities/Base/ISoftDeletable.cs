namespace SesliKitap.Data.Entities.Base;

/// <summary>
/// Yumuşak silme (soft delete) davranışını destekleyen varlıklar için sözleşme.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Kaydın silinmiş olarak işaretlenip işaretlenmediğini belirtir.
    /// </summary>
    bool SilindiMi { get; set; }

    /// <summary>
    /// Kaydın silindiği tarih ve saat (UTC).
    /// </summary>
    DateTime? SilinmeTarihi { get; set; }
}
