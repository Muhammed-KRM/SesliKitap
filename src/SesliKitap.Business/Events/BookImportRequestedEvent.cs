using SesliKitap.Data.Enums;

namespace SesliKitap.Business.Events;

/// <summary>
/// Kitap metin çıkarma, temizleme ve bölümleme parametreleri.
/// </summary>
/// <param name="UstAltBolgeOrani">Sayfa üst/alt bilgi aday bölgesi oranı (varsayılan 0.08).</param>
/// <param name="TekrarEsigi">Üst/alt bilgi tekrar eşiği oranı (varsayılan 0.30).</param>
/// <param name="MinBolumKelime">Birleştirme eşiği altındaki minimum bölüm kelime sayısı (varsayılan 300).</param>
/// <param name="SozdeBolumKelime">Bölüm başlığı bulunamadığında oluşturulacak sözde bölüm kelime hedefi (varsayılan 4000).</param>
public sealed record ProcessingSettings(
    double UstAltBolgeOrani = 0.08,
    double TekrarEsigi = 0.30,
    int MinBolumKelime = 300,
    int SozdeBolumKelime = 4000);

/// <summary>
/// Yeni yüklenen veya yeniden işlenecek bir kitabın Python Worker tarafından işlenmesi için yayınlanan olay.
/// </summary>
public sealed record BookImportRequestedEvent : IntegrationEvent
{
    /// <summary>
    /// İşlenecek kitabın benzersiz kimliği.
    /// </summary>
    public required Guid BookId { get; init; }

    /// <summary>
    /// Kitabın kaynağı (kullanıcı yüklemesi veya keşif).
    /// </summary>
    public required KaynakTipi KaynakTipi { get; init; }

    /// <summary>
    /// Kaynak dosyanın MinIO nesne anahtarı.
    /// </summary>
    public string? OrijinalDosyaYolu { get; init; }

    /// <summary>
    /// Keşiften içe aktarmada dosyanın indirileceği dış URL.
    /// </summary>
    public string? IndirmeUrl { get; init; }

    /// <summary>
    /// Kaynak dosyanın formatı.
    /// </summary>
    public required DosyaFormati DosyaFormati { get; init; }

    /// <summary>
    /// Beklenen dosya uzantısı/format kodu (<c>pdf</c>, <c>epub</c>, <c>txt</c>).
    /// </summary>
    public required string BeklenenFormat { get; init; }

    /// <summary>
    /// Metin temizleme ve bölümleme ayarları.
    /// </summary>
    public ProcessingSettings Ayarlar { get; init; } = new();
}
