using SesliKitap.Business.DTOs.Discovery;
using SesliKitap.Data.Enums;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Dış kitap arama sağlayıcısı (Internet Archive, Open Library, Gutendex, SearXNG) strateji sözleşmesi.
/// </summary>
public interface IBookSearchProvider
{
    /// <summary>Kısa sağlayıcı kodu (<c>ia</c>, <c>ol</c>, <c>gutenberg</c>, <c>web</c>).</summary>
    string Code { get; }

    /// <summary>Kullanıcıya gösterilen sağlayıcı adı.</summary>
    string DisplayName { get; }

    /// <summary>Veritabanı kaynak tipi karşılığı.</summary>
    KaynakTipi SourceType { get; }

    /// <summary>Sağlayıcının etkin olup olmadığı.</summary>
    bool IsEnabled { get; }

    /// <summary>Seçildiğinde telif uyarısı gösterilip gösterilmeyeceği.</summary>
    bool RequiresCopyrightWarning => false;

    /// <summary>
    /// Verilen sorgu için sağlayıcıda Türkçe kitap araması yapar.
    /// </summary>
    Task<IReadOnlyList<DiscoveryResultDto>> SearchAsync(string query, int page, CancellationToken ct = default);

    /// <summary>
    /// Sağlayıcıdaki belirli bir kitabın indirilebilir dosyalarını listeler.
    /// </summary>
    Task<IReadOnlyList<DiscoveryFileDto>> GetFilesAsync(string identifier, CancellationToken ct = default);
}
