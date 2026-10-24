using SesliKitap.Data.Enums;

namespace SesliKitap.Business.Events;

/// <summary>
/// Keşiften içe aktarılan kitabın kaynak dosyası Python Worker tarafından indirilip doğrulandıktan ve MinIO'ya kaydedildikten sonra yayınlanan olay.
/// </summary>
public sealed record BookSourceStoredEvent : IntegrationEvent
{
    /// <summary>Kitap kimliği.</summary>
    public required Guid BookId { get; init; }

    /// <summary>MinIO nesne anahtarı (<c>books/{bookId}/source/original.{ext}</c>).</summary>
    public required string OrijinalDosyaYolu { get; init; }

    /// <summary>Doğrulanan dosya formatı.</summary>
    public required DosyaFormati DosyaFormati { get; init; }

    /// <summary>İndirilen dosyanın bayt cinsinden boyutu.</summary>
    public required long DosyaBoyutu { get; init; }

    /// <summary>Dosyanın 64 karakterlik SHA-256 özeti.</summary>
    public required string DosyaHash { get; init; }
}
