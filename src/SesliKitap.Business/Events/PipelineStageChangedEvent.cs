namespace SesliKitap.Business.Events;

/// <summary>
/// Python Worker'ın kitap işleme veya seslendirme aşamalarındaki ilerlemeyi bildirdiği olay.
/// </summary>
public sealed record PipelineStageChangedEvent : IntegrationEvent
{
    /// <summary>
    /// İlgili kitabın kimliği.
    /// </summary>
    public Guid? BookId { get; init; }

    /// <summary>
    /// İlgili seslendirme işinin kimliği (varsa).
    /// </summary>
    public Guid? JobId { get; init; }

    /// <summary>
    /// İlgili bölümün kimliği (varsa).
    /// </summary>
    public Guid? ChapterId { get; init; }

    /// <summary>
    /// Mevcut pipeline aşaması adı (örn. <c>Indirme</c>, <c>MetinCikarma</c>, <c>Temizleme</c>, <c>BolumTespiti</c>).
    /// </summary>
    public required string Asama { get; init; }

    /// <summary>
    /// İlerleme yüzdesi (0–100).
    /// </summary>
    public int Yuzde { get; init; }

    /// <summary>
    /// Kullanıcıya gösterilecek durum mesajı.
    /// </summary>
    public required string Mesaj { get; init; }

    /// <summary>
    /// Aşamaya özgü ek ayrıntılar (örn. işlenen sayfa/toplam sayfa).
    /// </summary>
    public Dictionary<string, object?>? Detay { get; init; }
}
