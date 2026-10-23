using System.Text;
using SesliKitap.Data.Enums;

namespace SesliKitap.Business.Helpers;

/// <summary>
/// Yüklenen kitap ve ses dosyalarının ilk baytlarını (magic bytes) inceleyerek dosya uzantısıyla içerik uyumunu doğrulayan yardımcı sınıf.
/// </summary>
public static class FileSignatureValidator
{
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47];

    /// <summary>
    /// Akışın ilk baytlarına bakarak beklenen kitap formatını doğrular. Akış arama destekliyorsa konumunu sıfırlar.
    /// </summary>
    /// <param name="stream">Okunacak dosya akışı.</param>
    /// <param name="extension">Dosya uzantısı (örn. <c>.pdf</c>, <c>.epub</c>, <c>.txt</c>).</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>İçerik uzantıyla uyumluysa <see cref="DosyaFormati"/>, aksi halde <c>null</c>.</returns>
    public static async Task<DosyaFormati?> DetectBookFormatAsync(
        Stream stream,
        string extension,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var expected = ContentTypeMap.TryParseBookFormat(extension);
        if (expected is null)
        {
            return null;
        }

        var buffer = new byte[Math.Min(65536, stream.CanSeek && stream.Length > 0 ? (int)Math.Min(stream.Length, 65536) : 65536)];
        if (buffer.Length == 0)
        {
            return null;
        }

        var totalRead = await ReadBufferAsync(stream, buffer, ct);
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        if (totalRead == 0)
        {
            return null;
        }

        var span = buffer.AsSpan(0, totalRead);

        return expected.Value switch
        {
            DosyaFormati.Pdf => IsPdf(span) ? DosyaFormati.Pdf : null,
            DosyaFormati.Epub => IsEpub(span) ? DosyaFormati.Epub : null,
            DosyaFormati.Txt => IsPlainText(span) ? DosyaFormati.Txt : null,
            _ => null
        };
    }

    /// <summary>
    /// Akışın desteklenen bir ses formatı (WAV, MP3, OGG, WEBM/MKV, M4A/MP4, FLAC) olup olmadığını doğrular.
    /// </summary>
    /// <param name="stream">Okunacak ses akışı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Desteklenen ses imzasına sahipse <c>true</c>, aksi halde <c>false</c>.</returns>
    public static async Task<bool> IsSupportedAudioAsync(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var buffer = new byte[64];
        var totalRead = await ReadBufferAsync(stream, buffer, ct);
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        if (totalRead < 4)
        {
            return false;
        }

        var span = buffer.AsSpan(0, totalRead);

        // WAV: "RIFF" .... "WAVE"
        if (totalRead >= 12 &&
            span[..4].SequenceEqual("RIFF"u8) &&
            span.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            return true;
        }

        // OGG: "OggS"
        if (span[..4].SequenceEqual("OggS"u8))
        {
            return true;
        }

        // FLAC: "fLaC"
        if (span[..4].SequenceEqual("fLaC"u8))
        {
            return true;
        }

        // WEBM / MKV: 1A 45 DF A3
        if (span[0] == 0x1A && span[1] == 0x45 && span[2] == 0xDF && span[3] == 0xA3)
        {
            return true;
        }

        // M4A / MP4: "ftyp" at offset 4
        if (totalRead >= 8 && span.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return true;
        }

        // MP3: "ID3" veya MPEG frame sync (0xFF ve üst 3 bit 1)
        if (span[..3].SequenceEqual("ID3"u8) || (span[0] == 0xFF && (span[1] & 0xE0) == 0xE0))
        {
            return true;
        }

        return false;
    }

    private static bool IsPdf(ReadOnlySpan<byte> span)
    {
        var searchLen = Math.Min(span.Length, 1024);
        return span[..searchLen].IndexOf(PdfMagic) >= 0;
    }

    private static bool IsEpub(ReadOnlySpan<byte> span)
    {
        if (span.Length < 30 || !span[..4].SequenceEqual(ZipMagic))
        {
            return false;
        }

        var ascii = Encoding.ASCII.GetString(span[..Math.Min(span.Length, 4096)]);
        return ascii.Contains("application/epub+zip", StringComparison.OrdinalIgnoreCase) ||
               ascii.Contains("META-INF/container.xml", StringComparison.OrdinalIgnoreCase) ||
               ascii.Contains(".opf", StringComparison.OrdinalIgnoreCase) ||
               ascii.Contains("mimetype", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlainText(ReadOnlySpan<byte> span)
    {
        // Bilinen ikili dosya imzalarını reddet
        if (span.Length >= 4 &&
            (IsPdf(span) ||
             span[..4].SequenceEqual(ZipMagic) ||
             span[..3].SequenceEqual(JpegMagic) ||
             span[..4].SequenceEqual(PngMagic)))
        {
            return false;
        }

        // UTF-16 LE (FF FE) veya UTF-16 BE (FE FF) BOM varsa NUL baytları normaldir
        if (span.Length >= 2 &&
            ((span[0] == 0xFF && span[1] == 0xFE) || (span[0] == 0xFE && span[1] == 0xFF)))
        {
            return true;
        }

        // UTF-8 / Windows-1254 düz metinde NUL (0x00) baytı bulunmamalıdır
        return span.IndexOf((byte)0x00) < 0;
    }

    private static async Task<int> ReadBufferAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }
}
