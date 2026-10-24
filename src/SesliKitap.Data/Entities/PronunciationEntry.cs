using SesliKitap.Data.Entities.Base;

namespace SesliKitap.Data.Entities;

/// <summary>
/// Seslendirme öncesi özel kelime okunuşlarını tanımlayan telaffuz sözlüğü girdisi (Faz 8).
/// </summary>
public sealed class PronunciationEntry : AuditableEntity
{
    /// <summary>
    /// Metinde aranacak kelime veya ifade (1–100 karakter, benzersiz).
    /// </summary>
    public string Kelime { get; set; } = string.Empty;

    /// <summary>
    /// TTS motoruna gönderilecek Türkçe fonetik okunuş karşılığı (1–200 karakter).
    /// </summary>
    public string Okunus { get; set; } = string.Empty;

    /// <summary>
    /// Eşleşmenin büyük/küçük harf duyarlı olup olmadığı.
    /// </summary>
    public bool BuyukKucukDuyarli { get; set; }

    /// <summary>
    /// Bu sözlük kuralının aktif olup olmadığı.
    /// </summary>
    public bool AktifMi { get; set; } = true;

    /// <summary>
    /// İsteğe bağlı açıklama veya not.
    /// </summary>
    public string? Aciklama { get; set; }
}
