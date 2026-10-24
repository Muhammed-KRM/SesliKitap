using System.ComponentModel.DataAnnotations;

namespace SesliKitap.Business.Options;

/// <summary>
/// Uygulama içi kitap keşfi (<c>Discovery</c>) yapılandırma seçenekleri.
/// </summary>
public sealed class DiscoveryOptions
{
    /// <summary>Yapılandırma bölümü adı.</summary>
    public const string SectionName = "Discovery";

    /// <summary>Her bir arama sağlayıcısı için zaman aşımı süresi (saniye).</summary>
    [Range(1, 60)]
    public int ProviderTimeoutSec { get; init; } = 8;

    /// <summary>Sağlayıcı arama sonuçlarının Redis'te önbellekleme süresi (saat).</summary>
    [Range(1, 168)]
    public int ResultCacheHours { get; init; } = 24;

    /// <summary>Genel web araması (SearXNG) ayarları.</summary>
    public WebSearchOptions WebSearch { get; init; } = new();
}

/// <summary>
/// SearXNG genel web araması yapılandırması.
/// </summary>
public sealed class WebSearchOptions
{
    /// <summary>Web aramasının etkin olup olmadığı (varsayılan: <c>false</c>).</summary>
    public bool Enabled { get; init; }

    /// <summary>SearXNG servis adresi.</summary>
    public string SearxngUrl { get; init; } = "http://searxng:8080";
}
