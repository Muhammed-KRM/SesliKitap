using System.Globalization;
using Microsoft.Extensions.Primitives;

namespace SesliKitap.API.Streaming;

/// <summary>
/// Ayrıştırılmış HTTP <c>Range</c> bayt aralığı sonucu (Bölüm 4.15.3).
/// </summary>
/// <param name="Start">Başlangıç bayt indeksi (0 tabanlı, dahil).</param>
/// <param name="End">Bitiş bayt indeksi (0 tabanlı, dahil).</param>
/// <param name="IsSatisfiable">Aralığın dosya boyutu içinde karşılanabilir olup olmadığı.</param>
public sealed record ByteRange(long Start, long End, bool IsSatisfiable)
{
    /// <summary>Aralıktaki toplam bayt uzunluğu.</summary>
    public long Length => IsSatisfiable ? End - Start + 1 : 0;
}

/// <summary>
/// RFC 7233 tek aralık (<c>bytes=</c>) ayrıştırıcısı. Çoklu aralıklar veya geçersiz sözdizimi <c>null</c> (tam dosya) döndürür (Bölüm 4.15.3).
/// </summary>
public static class RangeRequestParser
{
    private const string BytesPrefix = "bytes=";

    /// <summary>
    /// <c>Range</c> HTTP başlığını dosya toplam uzunluğuna göre ayrıştırır.
    /// </summary>
    /// <param name="header">İstekteki <c>Range</c> başlık değeri.</param>
    /// <param name="totalLength">Dosyanın toplam bayt uzunluğu.</param>
    /// <returns>Tekil aralık için <see cref="ByteRange"/>, başlık yoksa veya geçersizse <c>null</c>.</returns>
    public static ByteRange? Parse(StringValues header, long totalLength)
    {
        if (totalLength <= 0 || StringValues.IsNullOrEmpty(header) || header.Count != 1)
        {
            return null;
        }

        var raw = header[0]?.Trim();
        if (string.IsNullOrEmpty(raw) ||
            !raw.StartsWith(BytesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var spec = raw[BytesPrefix.Length..].Trim();
        if (string.IsNullOrEmpty(spec) || spec.Contains(',', StringComparison.Ordinal))
        {
            return null;
        }

        var dashIndex = spec.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex < 0)
        {
            return null;
        }

        var startPart = spec[..dashIndex].Trim();
        var endPart = spec[(dashIndex + 1)..].Trim();

        if (startPart.Length == 0 && endPart.Length == 0)
        {
            return null;
        }

        // Sonek aralığı: bytes=-500 (son 500 bayt)
        if (startPart.Length == 0)
        {
            if (!long.TryParse(endPart, NumberStyles.None, CultureInfo.InvariantCulture, out var suffixLength) ||
                suffixLength < 0)
            {
                return null;
            }

            if (suffixLength == 0)
            {
                return new ByteRange(0, -1, IsSatisfiable: false);
            }

            var start = Math.Max(0L, totalLength - suffixLength);
            return new ByteRange(start, totalLength - 1, IsSatisfiable: true);
        }

        if (!long.TryParse(startPart, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedStart) ||
            parsedStart < 0)
        {
            return null;
        }

        if (parsedStart >= totalLength)
        {
            return new ByteRange(parsedStart, totalLength - 1, IsSatisfiable: false);
        }

        // Açık uçlu aralık: bytes=100-
        if (endPart.Length == 0)
        {
            return new ByteRange(parsedStart, totalLength - 1, IsSatisfiable: true);
        }

        if (!long.TryParse(endPart, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedEnd) ||
            parsedEnd < 0)
        {
            return null;
        }

        if (parsedEnd < parsedStart)
        {
            return null;
        }

        var clampedEnd = Math.Min(parsedEnd, totalLength - 1);
        return new ByteRange(parsedStart, clampedEnd, IsSatisfiable: true);
    }
}
