using System.Text.Json;
using SesliKitap.Business.DTOs.Discovery;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Enums;

namespace SesliKitap.Business.Infrastructure.Discovery;

/// <summary>
/// Open Library (<c>openlibrary.org</c>) Türkçe kitap arama sağlayıcısı; indirilebilir dosyaları <see cref="InternetArchiveSearchProvider"/> üzerinden çözer.
/// </summary>
public sealed class OpenLibrarySearchProvider : IBookSearchProvider
{
    private readonly HttpClient _http;
    private readonly InternetArchiveSearchProvider _iaProvider;

    /// <summary>
    /// <see cref="OpenLibrarySearchProvider"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public OpenLibrarySearchProvider(HttpClient http, InternetArchiveSearchProvider iaProvider)
    {
        _http = http;
        _iaProvider = iaProvider;

        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri("https://openlibrary.org/");
        }

        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("SesliKitap/1.0 (kisisel kullanim)");
        }
    }

    /// <inheritdoc />
    public string Code => "ol";

    /// <inheritdoc />
    public string DisplayName => "Open Library";

    /// <inheritdoc />
    public KaynakTipi SourceType => KaynakTipi.OpenLibrary;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveryResultDto>> SearchAsync(string query, int page, CancellationToken ct = default)
    {
        var safePage = Math.Max(1, page);
        var url =
            $"search.json?q={Uri.EscapeDataString(query)}&language=tur&fields=key,title,author_name,first_publish_year,cover_i,ia,ebook_access&limit=20&page={safePage}";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("docs", out var docsEl) || docsEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<DiscoveryResultDto>();
        foreach (var item in docsEl.EnumerateArray())
        {
            var access = item.TryGetProperty("ebook_access", out var accEl) && accEl.ValueKind == JsonValueKind.String
                ? accEl.GetString()
                : null;
            if (!string.Equals(access, "public", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var iaId = ReadFirstArrayString(item, "ia");
            var title = item.TryGetProperty("title", out var tEl) && tEl.ValueKind == JsonValueKind.String
                ? tEl.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(iaId) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var author = ReadFirstArrayString(item, "author_name");
            int? year = item.TryGetProperty("first_publish_year", out var yEl) && yEl.ValueKind == JsonValueKind.Number && yEl.TryGetInt32(out var yVal)
                ? yVal
                : null;
            string? coverUrl = item.TryGetProperty("cover_i", out var cEl) && cEl.ValueKind == JsonValueKind.Number && cEl.TryGetInt64(out var cId)
                ? $"https://covers.openlibrary.org/b/id/{cId}-M.jpg"
                : null;
            var key = item.TryGetProperty("key", out var kEl) && kEl.ValueKind == JsonValueKind.String
                ? kEl.GetString()
                : null;
            var pageUrl = !string.IsNullOrWhiteSpace(key)
                ? $"https://openlibrary.org{key}"
                : $"https://archive.org/details/{Uri.EscapeDataString(iaId)}";

            results.Add(new DiscoveryResultDto(
                SaglayiciKodu: Code,
                KaynakTipi: SourceType,
                KaynakKimligi: iaId,
                Baslik: title,
                Yazar: author,
                YayinYili: year,
                Dil: "tr",
                KapakUrl: coverUrl,
                SayfaUrl: pageUrl,
                IndirmeSayisi: 0,
                MevcutFormatlar: [DosyaFormati.Epub, DosyaFormati.Pdf],
                Dosyalar: [],
                Kaynaklar: [new DiscoverySourceBadgeDto(Code, SourceType, iaId, pageUrl)],
                Puan: 0));
        }

        return results;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DiscoveryFileDto>> GetFilesAsync(string identifier, CancellationToken ct = default)
    {
        return _iaProvider.GetFilesAsync(identifier, ct);
    }

    private static string? ReadFirstArrayString(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
            }
        }

        return null;
    }
}
