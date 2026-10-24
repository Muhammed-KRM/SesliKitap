namespace SesliKitap.Business.Events;

/// <summary>
/// Yeni bir ses profili yüklendiğinde veya örnek sesi yeniden üretilmek istendiğinde Python Worker'a gönderilen olay (Bölüm 4.7.1).
/// </summary>
public sealed record VoiceProfileUploadedEvent : IntegrationEvent
{
    /// <summary>Ses profilinin benzersiz kimliği.</summary>
    public required Guid VoiceProfileId { get; init; }

    /// <summary>Ham ses dosyasının MinIO nesne anahtarı (<c>voices/{id}/raw.{ext}</c>).</summary>
    public required string HamSesYolu { get; init; }

    /// <summary>Referans ses kaydında okunan metnin transkripti (varsa).</summary>
    public string? ReferansMetin { get; init; }

    /// <summary>Örnek ses üretimi için okunacak Türkçe metin.</summary>
    public required string OrnekMetin { get; init; }
}
