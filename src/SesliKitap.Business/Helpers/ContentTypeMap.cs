using SesliKitap.Data.Enums;

namespace SesliKitap.Business.Helpers;

/// <summary>
/// Dosya uzantıları, format enum'ları ve MIME içerik türleri arasındaki eşlemeleri yöneten yardımcı sınıf.
/// </summary>
public static class ContentTypeMap
{
    /// <summary>
    /// Dosya uzantısından MIME içerik türünü döner.
    /// </summary>
    public static string GetContentType(string extensionOrFileName)
    {
        var ext = Path.GetExtension(extensionOrFileName);
        if (string.IsNullOrEmpty(ext))
        {
            ext = extensionOrFileName;
        }

        return ext.TrimStart('.').ToLowerInvariant() switch
        {
            "pdf" => "application/pdf",
            "epub" => "application/epub+zip",
            "txt" => "text/plain; charset=utf-8",
            "ogg" or "opus" => "audio/ogg",
            "mp3" => "audio/mpeg",
            "wav" => "audio/wav",
            "flac" => "audio/flac",
            "m4a" => "audio/mp4",
            "m4b" => "audio/mp4",
            "webm" => "audio/webm",
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "json" => "application/json",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// <see cref="DosyaFormati"/> değerinden noktasız dosya uzantısını döner (<c>pdf</c>, <c>epub</c>, <c>txt</c>).
    /// </summary>
    public static string ToExtension(DosyaFormati format) => format switch
    {
        DosyaFormati.Pdf => "pdf",
        DosyaFormati.Epub => "epub",
        DosyaFormati.Txt => "txt",
        _ => "bin"
    };

    /// <summary>
    /// Dosya uzantısından <see cref="DosyaFormati"/> değerini çözümler.
    /// </summary>
    public static DosyaFormati? TryParseBookFormat(string? extensionOrFileName)
    {
        if (string.IsNullOrWhiteSpace(extensionOrFileName))
        {
            return null;
        }

        var ext = Path.GetExtension(extensionOrFileName);
        if (string.IsNullOrEmpty(ext))
        {
            ext = extensionOrFileName;
        }

        return ext.TrimStart('.').ToLowerInvariant() switch
        {
            "pdf" => DosyaFormati.Pdf,
            "epub" => DosyaFormati.Epub,
            "txt" => DosyaFormati.Txt,
            _ => null
        };
    }
}
