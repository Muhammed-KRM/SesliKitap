namespace SesliKitap.Data.Enums;

/// <summary>
/// Bir kitap bölümünün seslendirme durumu.
/// </summary>
public enum BolumSesDurumu
{
    /// <summary>Bölüm için henüz hiç ses üretilmedi.</summary>
    Yok = 0,

    /// <summary>Bölüm sentez işi kuyruğuna alındı.</summary>
    Kuyrukta = 1,

    /// <summary>Bölüm şu anda TTS motorunda seslendiriliyor.</summary>
    Seslendiriliyor = 2,

    /// <summary>Bölüm sesi hazır ve mevcut metin sürümüyle uyumlu.</summary>
    Hazir = 3,

    /// <summary>Bölüm sesi mevcut fakat bölüm metni sonradan düzenlendiği için güncel değil.</summary>
    GuncelDegil = 4,

    /// <summary>Bölüm seslendirilirken hata oluştu.</summary>
    Hata = 9
}
