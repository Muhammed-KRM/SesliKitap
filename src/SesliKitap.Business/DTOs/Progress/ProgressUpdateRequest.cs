namespace SesliKitap.Business.DTOs.Progress;

/// <summary>
/// Dinleme konumu güncelleme isteği modeli.
/// </summary>
public sealed class ProgressUpdateRequest
{
    /// <summary>
    /// Dinlenen bölümün kimliği.
    /// </summary>
    public Guid ChapterId { get; set; }

    /// <summary>
    /// Bölüm içindeki konum (saniye).
    /// </summary>
    public double PozisyonSn { get; set; }

    /// <summary>
    /// Oynatma hızı (0.5 – 3.0).
    /// </summary>
    public double OynatmaHizi { get; set; } = 1.0;

    /// <summary>
    /// İstemci cihazın benzersiz kimliği.
    /// </summary>
    public string CihazKimligi { get; set; } = string.Empty;

    /// <summary>
    /// İstemci cihazın okunabilir adı (ör. "Windows · Chrome").
    /// </summary>
    public string? CihazAdi { get; set; }

    /// <summary>
    /// İstemcinin bu konum olayını ürettiği UTC zamanı (son-yazan-kazanır için).
    /// </summary>
    public DateTime IstemciZamani { get; set; }

    /// <summary>
    /// Kitabın son bölümünün bitirilip bitirilmediği.
    /// </summary>
    public bool TamamlandiMi { get; set; }
}
