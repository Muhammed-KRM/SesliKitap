namespace SesliKitap.Business.Events;

/// <summary>
/// Python Worker ses profilini hazırlayıp örnek sesi ürettiğinde (veya hata aldığında) yayınlanan olay (Bölüm 4.7.2).
/// </summary>
public sealed record VoiceProfileReadyEvent : IntegrationEvent
{
    /// <summary>Ses profilinin benzersiz kimliği.</summary>
    public required Guid VoiceProfileId { get; init; }

    /// <summary>Hazırlama ve örnek üretiminin başarılı olup olmadığını belirtir.</summary>
    public required bool Basarili { get; init; }

    /// <summary>Hazırlanmış 24 kHz mono WAV referans sesinin MinIO anahtarı (<c>voices/{id}/reference.wav</c>).</summary>
    public string? ReferansSesYolu { get; init; }

    /// <summary>Hazırlanmış WAV dosyasının SHA-256 özeti (64 karakter onaltılık).</summary>
    public string? ReferansHash { get; init; }

    /// <summary>Hazırlanmış referans sesin saniye cinsinden süresi.</summary>
    public double? ReferansSureSn { get; init; }

    /// <summary>Örnek ses dosyasının MinIO anahtarı (<c>voices/{id}/sample.ogg</c>).</summary>
    public string? OrnekSesYolu { get; init; }

    /// <summary>Örnek ses üretimi sırasında harcanan GPU süresi (milisaniye).</summary>
    public long GpuMs { get; init; }

    /// <summary>Başarısız ise kullanıcı dostu Türkçe hata açıklaması.</summary>
    public string? HataMesaji { get; init; }
}
