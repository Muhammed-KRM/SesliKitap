using System.Globalization;
using System.Text.Json;
using SesliKitap.Business.DTOs.Discovery;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Enums;

namespace SesliKitap.Business.Infrastructure.Discovery;

/// <summary>
/// Internet Archive (<c>archive.org</c>) Türkçe metin/kitap arama ve dosya listeleme sağlayıcısı.
/// </summary>
public sealed class InternetArchiveSearchProvider : IBookSearchProvider
{
    private readonly HttpClient _http;

    /// <summary>
    /// <see cref="InternetArchiveSearchProvider"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public InternetArchiveSearchProvider(HttpClient http)
    {
        _http = http;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri("https://archive.org/");
        }

        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("SesliKitap/1.0 (kisisel kullanim)");
        }
    }

    /// <inheritdoc />
    public string Code => "ia";

    /// <inheritdoc />
    public string DisplayName => "Internet Archive";

    /// <inheritdoc />
    public KaynakTipi SourceType => KaynakTipi.InternetArchive;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveryResultDto>> SearchAsync(string query, int page, CancellationToken ct = default)
    {
        var safePage = Math.Max(1, page);
        var escaped = query.Replace("\"", " ", StringComparison.Ordinal).Trim();
        var iaQuery = $"(title:({escaped}) OR creator:({escaped})) AND mediatype:texts AND language:(tur OR Turkish OR tr)";
        var url =
            $"advancedsearch.php?q={Uri.EscapeDataString(iaQuery)}&fl[]=identifier&fl[]=title&fl[]=creator&fl[]=year&fl[]=downloads&fl[]=format&fl[]=language&rows=20&page={safePage}&output=json";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("response", out var respEl) ||
            !respEl.TryGetProperty("docs", out var docsEl) ||
            docsEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<DiscoveryResultDto>();
        foreach (var item in docsEl.EnumerateArray())
        {
            var id = ReadString(item, "identifier");
            var title = ReadString(item, "title");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var creator = ReadFirstString(item, "creator");
            var year = ReadInt(item, "year");
            var downloads = ReadInt(item, "downloads") ?? 0;
            var formats = ParseFormats(item);
            if (formats.Count == 0)
            {
                formats = [DosyaFormati.Pdf];
            }

            var pageUrl = $"https://archive.org/details/{Uri.EscapeDataString(id)}";
            var coverUrl = $"https://archive.org/services/img/{Uri.EscapeDataString(id)}";

            results.Add(new DiscoveryResultDto(
                SaglayiciKodu: Code,
                KaynakTipi: SourceType,
                KaynakKimligi: id,
                Baslik: title,
                Yazar: creator,
                YayinYili: year,
                Dil: "tr",
                KapakUrl: coverUrl,
                SayfaUrl: pageUrl,
                IndirmeSayisi: downloads,
                MevcutFormatlar: formats,
                Dosyalar: [],
                Kaynaklar: [new DiscoverySourceBadgeDto(Code, SourceType, id, pageUrl)],
                Puan: 0));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveryFileDto>> GetFilesAsync(string identifier, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return [];
        }

        var url = $"metadata/{Uri.EscapeDataString(identifier)}";
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        if (root.TryGetProperty("is_dark", out var darkEl) &&
            darkEl.ValueKind == JsonValueKind.True)
        {
            return [];
        }

        if (root.TryGetProperty("metadata", out var metaEl) &&
            metaEl.TryGetProperty("collection", out var collEl) &&
            ContainsCollection(collEl, "inlibrary"))
        {
            return [];
        }

        if (!root.TryGetProperty("files", out var filesEl) || filesEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var files = new List<DiscoveryFileDto>();
        foreach (var f in filesEl.EnumerateArray())
        {
            var name = ReadString(f, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var formatStr = ReadString(f, "format") ?? string.Empty;
            var detected = ClassifyIaFile(name, formatStr);
            if (detected is null)
            {
                continue;
            }

            var size = ReadLong(f, "size");
            var downloadUrl = $"https://archive.org/download/{Uri.EscapeDataString(identifier)}/{Uri.EscapeDataString(name)}";
            files.Add(new DiscoveryFileDto(name, detected.Value, size, downloadUrl));
        }

        return files
            .OrderBy(x => x.Format == DosyaFormati.Epub ? 0 : x.Format == DosyaFormati.Pdf ? 1 : 2)
            .ToList();
    }

    private static DosyaFormati? ClassifyIaFile(string name, string format)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (ext == ".epub" || format.Equals("EPUB", StringComparison.OrdinalIgnoreCase))
        {
            return DosyaFormati.Epub;
        }

        if (ext == ".pdf" ||
            format.Equals("Text PDF", StringComparison.OrdinalIgnoreCase) ||
            format.Equals("Additional Text PDF", StringComparison.OrdinalIgnoreCase) ||
            format.Equals("Standard LuraTech PDF", StringComparison.OrdinalIgnoreCase))
        {
            return DosyaFormati.Pdf;
        }

        if (ext == ".txt" && !name.EndsWith("_djvu.txt", StringComparison.OrdinalIgnoreCase) &&
            !name.EndsWith("_meta.txt", StringComparison.OrdinalIgnoreCase))
        {
            return DosyaFormati.Txt;
        }

        if (format.Equals("DjVuTXT", StringComparison.OrdinalIgnoreCase))
        {
            return DosyaFormati.Txt;
        }

        return null;
    }

    private static List<DosyaFormati> ParseFormats(JsonElement item)
    {
        var set = new HashSet<DosyaFormati>();
        if (!item.TryGetProperty("format", out var fmtEl))
        {
            return [];
        }

        void AddFormat(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return;
            if (s.Contains("EPUB", StringComparison.OrdinalIgnoreCase)) set.Add(DosyaFormati.Epub);
            else if (s.Contains("PDF", StringComparison.OrdinalIgnoreCase)) set.Add(DosyaFormati.Pdf);
            else if (s.Contains("TXT", StringComparison.OrdinalIgnoreCase) || s.Contains("Text", StringComparison.OrdinalIgnoreCase)) set.Add(DosyaFormati.Txt);
        }

        if (fmtEl.ValueKind == JsonValueKind.String)
        {
            AddFormat(fmtEl.GetString());
        }
        else if (fmtEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in fmtEl.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    AddFormat(el.GetString());
                }
            }
        }

        return set.ToList();
    }

    private static bool ContainsCollection(JsonElement el, string target)
    {
        if (el.ValueKind == JsonValueKind.String)
        {
            return string.Equals(el.GetString(), target, StringComparison.OrdinalIgnoreCase);
        }

        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String &&
                    string.Equals(item.GetString(), target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? ReadString(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        return val.ValueKind == JsonValueKind.String ? val.GetString() : null;
    }

    private static string? ReadFirstString(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        if (val.ValueKind == JsonValueKind.String) return val.GetString();
        if (val.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in val.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String) return item.GetString();
            }
        }
        return null;
    }

    private static int? ReadInt(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var num)) return num;
        if (val.ValueKind == JsonValueKind.String &&
            int.TryParse(val.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
        return null;
    }

    private static long? ReadLong(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        if (val.ValueKind == JsonValueKind.Number && val.TryGetInt64(out var num)) return num;
        if (val.ValueKind == JsonValueKind.String &&
            long.TryParse(val.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
        return null;
    }
}
