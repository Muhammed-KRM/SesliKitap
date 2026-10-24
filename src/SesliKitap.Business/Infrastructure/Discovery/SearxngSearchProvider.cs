using System.Text.Json;
using Microsoft.Extensions.Options;
using SesliKitap.Business.DTOs.Discovery;
using SesliKitap.Business.Helpers;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;
using SesliKitap.Data.Enums;

namespace SesliKitap.Business.Infrastructure.Discovery;

/// <summary>
/// SearXNG genel web araması sağlayıcısı (varsayılan kapalı; <see cref="DiscoveryOptions.WebSearch"/> ile etkinleştirilir).
/// </summary>
public sealed class SearxngSearchProvider : IBookSearchProvider
{
    private readonly HttpClient _http;
    private readonly DiscoveryOptions _options;

    /// <summary>
    /// <see cref="SearxngSearchProvider"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public SearxngSearchProvider(HttpClient http, IOptions<DiscoveryOptions> options)
    {
        _http = http;
        _options = options.Value;

        if (_http.BaseAddress is null && Uri.TryCreate(_options.WebSearch.SearxngUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
        {
            _http.BaseAddress = baseUri;
        }

        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("SesliKitap/1.0 (kisisel kullanim)");
        }
    }

    /// <inheritdoc />
    public string Code => "web";

    /// <inheritdoc />
    public string DisplayName => "Web Araması";

    /// <inheritdoc />
    public KaynakTipi SourceType => KaynakTipi.Web;

    /// <inheritdoc />
    public bool IsEnabled => _options.WebSearch.Enabled;

    /// <inheritdoc />
    public bool RequiresCopyrightWarning => true;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveryResultDto>> SearchAsync(string query, int page, CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            return [];
        }

        var safePage = Math.Max(1, page);
        var searxQuery = $"\"{query.Trim()}\" (filetype:pdf OR filetype:epub)";
        var url = $"search?q={Uri.EscapeDataString(searxQuery)}&format=json&categories=general&pageno={safePage}";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("results", out var resultsEl) || resultsEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<DiscoveryResultDto>();
        foreach (var item in resultsEl.EnumerateArray())
        {
            var href = item.TryGetProperty("url", out var uEl) && uEl.ValueKind == JsonValueKind.String
                ? uEl.GetString()
                : null;
            var title = item.TryGetProperty("title", out var tEl) && tEl.ValueKind == JsonValueKind.String
                ? tEl.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            if (!Uri.TryCreate(href, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }

            var pathLower = uri.AbsolutePath.ToLowerInvariant();
            DosyaFormati format = pathLower.EndsWith(".epub", StringComparison.Ordinal)
                ? DosyaFormati.Epub
                : DosyaFormati.Pdf;
            var ext = format == DosyaFormati.Epub ? "epub" : "pdf";
            var idHash = HashHelper.Sha1Hex(href);
            var fileName = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrWhiteSpace(fileName) || !fileName.Contains('.', StringComparison.Ordinal))
            {
                fileName = $"web-{idHash[..8]}.{ext}";
            }

            var fileDto = new DiscoveryFileDto(fileName, format, null, href);
            list.Add(new DiscoveryResultDto(
                SaglayiciKodu: Code,
                KaynakTipi: SourceType,
                KaynakKimligi: idHash,
                Baslik: title,
                Yazar: uri.Host,
                YayinYili: null,
                Dil: "tr",
                KapakUrl: null,
                SayfaUrl: href,
                IndirmeSayisi: 0,
                MevcutFormatlar: [format],
                Dosyalar: [fileDto],
                Kaynaklar: [new DiscoverySourceBadgeDto(Code, SourceType, idHash, href)],
                Puan: 0,
                DogrulanmamisWebKaynagi: true));
        }

        return list;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DiscoveryFileDto>> GetFilesAsync(string identifier, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<DiscoveryFileDto>>([]);
    }
}
