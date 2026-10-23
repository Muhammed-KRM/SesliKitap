namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Birim testlerinde zamanı kontrol edebilmek için sistem saatini soyutlayan arayüz.
/// </summary>
public interface IClock
{
    /// <summary>Şu anki UTC tarih ve saat bilgisini döner.</summary>
    DateTimeOffset UtcNow { get; }
}
