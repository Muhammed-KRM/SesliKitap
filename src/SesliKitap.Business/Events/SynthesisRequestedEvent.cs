namespace SesliKitap.Business.Events;

/// <summary>
/// Bir kitabın seslendirme (sentez) işi başlatıldığında Python Worker'a gönderilen olay (Bölüm 4.7.1).
/// </summary>
public sealed record SynthesisRequestedEvent : IntegrationEvent
{
    /// <summary>Sentez işi kimliği.</summary>
    public required Guid JobId { get; init; }

    /// <summary>Kitap kimliği.</summary>
    public required Guid BookId { get; init; }

    /// <summary>MinIO üzerindeki <c>synthesis/{jobId}/input.json</c> nesne anahtarı.</summary>
    public required string GirdiYolu { get; init; }
}
