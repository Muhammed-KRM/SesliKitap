namespace SesliKitap.Data.Enums;

/// <summary>
/// Ses profilinin hazırlanma ve kullanılabilirlik durumu (Bölüm 4.5).
/// </summary>
public enum SesProfiliDurumu
{
    /// <summary>Referans ses temizleniyor ve örnek ses üretiliyor.</summary>
    Hazirlaniyor = 0,

    /// <summary>Ses profili kitap seslendirme işlerinde kullanıma hazır.</summary>
    Hazir = 1,

    /// <summary>Referans ses işlenirken veya örnek üretilirken hata oluştu.</summary>
    Hata = 2
}
