using SesliKitap.Data.Entities.Base;
using SesliKitap.Data.Enums;

namespace SesliKitap.Data.Entities;

/// <summary>
/// Ses klonlama ve TTS sentezinde kullanılan ses profilini temsil eden varlık (Bölüm 4.6.2).
/// </summary>
public sealed class VoiceProfile : AuditableEntity, ISoftDeletable
{
    /// <summary>Ses profilinin benzersiz görünen adı.</summary>
    public string Ad { get; set; } = string.Empty;

    /// <summary>Ses profili açıklaması veya notu.</summary>
    public string? Aciklama { get; set; }

    /// <summary>Konuşmacı cinsiyeti.</summary>
    public Cinsiyet? Cinsiyet { get; set; }

    /// <summary>Yüklenen ham ses dosyasının MinIO nesne anahtarı (<c>voices/{id}/raw.{ext}</c>).</summary>
    public string HamSesYolu { get; set; } = string.Empty;

    /// <summary>24 kHz mono WAV olarak hazırlanmış referans sesin MinIO nesne anahtarı (<c>voices/{id}/reference.wav</c>).</summary>
    public string? ReferansSesYolu { get; set; }

    /// <summary>Hazırlanmış WAV dosyasının SHA-256 özeti (64 karakter onaltılık).</summary>
    public string? ReferansHash { get; set; }

    /// <summary>Referans ses kaydında okunan metnin transkripti.</summary>
    public string? ReferansMetin { get; set; }

    /// <summary>Hazırlanmış referans sesin saniye cinsinden süresi.</summary>
    public double? ReferansSureSn { get; set; }

    /// <summary>Örnek Türkçe paragraf sesinin MinIO nesne anahtarı (<c>voices/{id}/sample.ogg</c>).</summary>
    public string? OrnekSesYolu { get; set; }

    /// <summary>VoxCPM2 / TTS model sentez parametreleri (<c>jsonb</c>).</summary>
    public VoiceParameters Parametreler { get; set; } = new();

    /// <summary>Sistemdeki varsayılan ses profili olup olmadığını belirtir.</summary>
    public bool VarsayilanMi { get; set; }

    /// <summary>Ses sahibinin etik kullanım onayının alındığını belirtir (zorunlu <c>true</c>).</summary>
    public bool KullanimOnayi { get; set; }

    /// <summary>Ses profilinin hazırlık durumu.</summary>
    public SesProfiliDurumu Durum { get; set; }

    /// <summary>Hazırlık sırasında oluşan son hata mesajı (varsa).</summary>
    public string? HataMesaji { get; set; }

    /// <summary>Örnek ses üretimi için harcanan toplam GPU süresi (milisaniye).</summary>
    public long GpuMs { get; set; }

    /// <summary>Kaydın yumuşak silinmiş olup olmadığını belirtir.</summary>
    public bool SilindiMi { get; set; }

    /// <summary>Kaydın silinme zamanı (UTC).</summary>
    public DateTime? SilinmeTarihi { get; set; }
}

/// <summary>
/// Ses profiline ait TTS model parametreleri (<c>jsonb</c> olarak saklanır).
/// </summary>
public sealed class VoiceParameters
{
    /// <summary>Classifier-Free Guidance (CFG) katsayısı (varsayılan: 2.0).</summary>
    public double CfgValue { get; set; } = 2.0;

    /// <summary>Difüzyon çıkarım adım sayısı (varsayılan: 10).</summary>
    public int InferenceTimesteps { get; set; } = 10;

    /// <summary>Konuşma hızı çarpanı (varsayılan: 1.0).</summary>
    public double KonusmaHizi { get; set; } = 1.0;

    /// <summary>Deterministik üretim için rastgelelik tohumu (opsiyonel).</summary>
    public int? Tohum { get; set; }
}
