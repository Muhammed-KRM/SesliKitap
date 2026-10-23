namespace SesliKitap.Business.Events;

/// <summary>
/// Servisler arası (.NET ↔ Python Worker) tüm entegrasyon olaylarının ortak taban kaydı.
/// </summary>
public abstract record IntegrationEvent
{
    /// <summary>
    /// Mesaj şeması sürüm numarası (şu an 1).
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Uçtan uca izleme kimliği (HTTP isteğinden taşınır).
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Olayın gerçekleştiği UTC zamanı.
    /// </summary>
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}
