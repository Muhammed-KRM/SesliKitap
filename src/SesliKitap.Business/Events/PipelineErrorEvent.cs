namespace SesliKitap.Business.Events;

/// <summary>
/// Python Worker'da kitap işleme, ses profili hazırlama veya seslendirme sırasında hata oluştuğunda yayınlanan olay.
/// </summary>
public sealed record PipelineErrorEvent : IntegrationEvent
{
    /// <summary>
    /// İlgili kitabın kimliği (varsa).
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
    /// İlgili ses profilinin kimliği (varsa).
    /// </summary>
    public Guid? VoiceProfileId { get; init; }

    /// <summary>
    /// Hatanın oluştuğu pipeline aşaması.
    /// </summary>
    public required string Asama { get; init; }

    /// <summary>
    /// Makine tarafından okunabilir hata kodu (örn. <c>PDF_ENCRYPTED</c>, <c>UNSUPPORTED_FORMAT</c>).
    /// </summary>
    public required string HataKodu { get; init; }

    /// <summary>
    /// Kullanıcıya gösterilecek anlaşılır Türkçe hata mesajı.
    /// </summary>
    public required string HataMesaji { get; init; }

    /// <summary>
    /// Hatanın yeniden deneme ile kurtarılabilir olup olmadığı.
    /// </summary>
    public bool Kurtarilabilir { get; init; }
}
