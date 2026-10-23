using System.ComponentModel.DataAnnotations;

namespace SesliKitap.Business.Options;

/// <summary>
/// MinIO S3 uyumlu nesne depolama yapılandırma seçenekleri (<c>Minio</c> bölümü).
/// </summary>
public sealed class StorageOptions
{
    /// <summary>Yapılandırma bölüm adı.</summary>
    public const string SectionName = "Minio";

    /// <summary>MinIO sunucu uç noktası (örn. <c>localhost:9010</c>).</summary>
    [Required]
    public string Endpoint { get; set; } = "localhost:9010";

    /// <summary>MinIO erişim anahtarı (Access Key).</summary>
    [Required]
    public string AccessKey { get; set; } = "minioadmin";

    /// <summary>MinIO gizli anahtarı (Secret Key).</summary>
    [Required]
    public string SecretKey { get; set; } = "minioadmin";

    /// <summary>Varsayılan bucket adı.</summary>
    [Required]
    public string BucketName { get; set; } = "seslikitap";

    /// <summary>HTTPS/SSL bağlantı kullanımı.</summary>
    public bool UseSsl { get; set; }
}
