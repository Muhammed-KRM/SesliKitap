using System.ComponentModel.DataAnnotations;

namespace SesliKitap.Business.Options;

/// <summary>
/// Kitap ve ses profili dosya yükleme limitleri ve izin verilen uzantı ayarları (<c>Upload</c> bölümü).
/// </summary>
public sealed class UploadOptions
{
    /// <summary>
    /// Yapılandırma bölüm adı.
    /// </summary>
    public const string SectionName = "Upload";

    /// <summary>
    /// Maksimum kitap dosya boyutu (varsayılan 300 MB = 314.572.800 bayt).
    /// </summary>
    [Range(1024 * 1024, 1024L * 1024 * 1024)]
    public long MaxBookBytes { get; init; } = 314_572_800;

    /// <summary>
    /// Maksimum ses profili dosya boyutu (varsayılan 20 MB = 20.971.520 bayt).
    /// </summary>
    [Range(1024 * 100, 100L * 1024 * 1024)]
    public long MaxVoiceBytes { get; init; } = 20_971_520;

    /// <summary>
    /// İzin verilen kitap dosya uzantıları.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string[] AllowedBookExtensions { get; init; } = [".pdf", ".epub", ".txt"];

    /// <summary>
    /// İzin verilen ses profili dosya uzantıları.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string[] AllowedVoiceExtensions { get; init; } = [".wav", ".mp3", ".m4a", ".ogg", ".webm", ".flac"];
}
