using System.Globalization;
using System.Text.Json;
using SesliKitap.Business.DTOs.Discovery;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Enums;

namespace SesliKitap.Business.Infrastructure.Discovery;

/// <summary>
/// Project Gutenberg (Gutendex API — <c>gutendex.com</c>) Türkçe kitap arama sağlayıcısı.
/// </summary>
public sealed class GutendexSearchProvider : IBookSearchProvider
{
    private readonly HttpClient _http;

    /// <summary>
    /// <see cref="GutendexSearchProvider"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public GutendexSearchProvider(HttpClient http)
    {
        _http = http;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri("https://gutendex.com/");
        }

        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("SesliKitap/1.0 (kisisel kullanim)");
        }
    }

    /// <inheritdoc />
    public string Code => "gutenberg";

    /// <inheritdoc />
    public string DisplayName => "Project Gutenberg";

    /// <inheritdoc />
    public KaynakTipi SourceType => KaynakTipi.Gutenberg;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveryResultDto>> SearchAsync(string query, int page, CancellationToken ct = default)
    {
        var safePage = Math.Max(1, page);
        var url = $"books?search={Uri.EscapeDataString(query)}&languages=tr&page={safePage}";

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
            if (!item.TryGetProperty("id", out var idEl) || !idEl.TryGetInt32(out var idNum))
            {
                continue;
            }

            var idStr = idNum.ToString(CultureInfo.InvariantCulture);
            var title = item.TryGetProperty("title", out var tEl) && tEl.ValueKind == JsonValueKind.String
                ? tEl.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var author = ExtractFirstAuthor(item);
            var downloads = item.TryGetProperty("download_count", out var dlEl) && dlEl.TryGetInt32(out var dlVal)
                ? dlVal
                : 0;
            var (files, coverUrl) = ExtractFilesAndCover(idStr, item);
            if (files.Count == 0)
            {
                continue;
            }

            var formats = files.Select(f => f.Format).Distinct().ToList();
            var pageUrl = $"https://www.gutenberg.org/ebooks/{idStr}";

            list.Add(new DiscoveryResultDto(
                SaglayiciKodu: Code,
                KaynakTipi: SourceType,
                KaynakKimligi: idStr,
                Baslik: title,
                Yazar: author,
                YayinYili: null,
                Dil: "tr",
                KapakUrl: coverUrl,
                SayfaUrl: pageUrl,
                IndirmeSayisi: downloads,
                MevcutFormatlar: formats,
                Dosyalar: files,
                Kaynaklar: [new DiscoverySourceBadgeDto(Code, SourceType, idStr, pageUrl)],
                Puan: 0));
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveryFileDto>> GetFilesAsync(string identifier, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return [];
        }

        var url = $"books/{Uri.EscapeDataString(identifier)}";
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var (files, _) = ExtractFilesAndCover(identifier, doc.RootElement);
        return files;
    }

    private static string? ExtractFirstAuthor(JsonElement item)
    {
        if (!item.TryGetProperty("authors", out var authorsEl) || authorsEl.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var a in authorsEl.EnumerateArray())
        {
            if (a.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String)
            {
                return nEl.GetString();
            }
        }

        return null;
    }

    private static (List<DiscoveryFileDto> Files, string? CoverUrl) ExtractFilesAndCover(string idStr, JsonElement item)
    {
        var files = new List<DiscoveryFileDto>();
        string? coverUrl = null;

        if (!item.TryGetProperty("formats", out var fmtObj) || fmtObj.ValueKind != JsonValueKind.Object)
        {
            return (files, coverUrl);
        }

        foreach (var prop in fmtObj.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var href = prop.Value.GetString();
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var mime = prop.Name.ToLowerInvariant();
            if (mime.StartsWith("image/jpeg", StringComparison.Ordinal))
            {
                coverUrl ??= href;
                continue;
            }

            if (mime.StartsWith("application/epub+zip", StringComparison.Ordinal))
            {
                files.Add(new DiscoveryFileDto($"gutenberg-{idStr}.epub", DosyaFormati.Epub, null, href));
            }
            else if (mime.StartsWith("application/pdf", StringComparison.Ordinal))
            {
                files.Add(new DiscoveryFileDto($"gutenberg-{idStr}.pdf", DosyaFormati.Pdf, null, href));
            }
            else if (mime.StartsWith("text/plain", StringComparison.Ordinal) && !href.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(new DiscoveryFileDto($"gutenberg-{idStr}.txt", DosyaFormati.Txt, null, href));
            }
        }

        return (files, coverUrl);
    }
}
