namespace SesliKitap.Business.Events;

/// <summary>
/// Python Worker tarafından M4B dışa aktarım üretimi tamamlandığında yayınlanan olay (Faz 8).
/// </summary>
public sealed record BookExportCompletedEvent : IntegrationEvent
{
    /// <summary>İlgili kitabın kimliği.</summary>
    public required Guid BookId { get; init; }

    /// <summary>Dışa aktarım formatı (<c>m4b</c>).</summary>
    public string Format { get; init; } = "m4b";

    /// <summary>Üretimin başarılı olup olmadığı.</summary>
    public required bool Basarili { get; init; }

    /// <summary>MinIO nesne anahtarı.</summary>
    public string? DosyaYolu { get; init; }

    /// <summary>Dosya boyutu (bayt).</summary>
    public long Boyut { get; init; }

    /// <summary>Hata oluştuysa açıklaması.</summary>
    public string? HataMesaji { get; init; }
}
