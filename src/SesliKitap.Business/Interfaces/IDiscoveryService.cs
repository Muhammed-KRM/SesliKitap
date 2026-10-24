using SesliKitap.Business.DTOs.Discovery;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Çoklu kitap arama sağlayıcılarını paralel sorgulayan, sonuçları tekilleştiren ve puanlayan keşif servisi sözleşmesi.
/// </summary>
public interface IDiscoveryService
{
    /// <summary>
    /// Kullanılabilir keşif sağlayıcılarını listeler.
    /// </summary>
    IReadOnlyList<DiscoveryProviderDto> GetProviders();

    /// <summary>
    /// Seçili sağlayıcılarda paralel kitap araması yapar, sonuçları tekilleştirir ve puanlayarak sıralar.
    /// </summary>
    Task<IReadOnlyList<DiscoveryResultDto>> SearchAsync(
        string query,
        IReadOnlyList<string>? providerCodes,
        int page,
         string? traceId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Belirtilen sağlayıcı ve kaynak kimliği için indirilebilir dosyaları getirir.
    /// </summary>
    Task<IReadOnlyList<DiscoveryFileDto>> GetFilesAsync(
        string providerCode,
        string identifier,
        CancellationToken ct = default);
}
