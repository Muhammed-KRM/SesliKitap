namespace SesliKitap.Data.Enums;

/// <summary>
/// Sentez (seslendirme) işinin yaşam döngüsü durumlarını tanımlar (Bölüm 4.5.3).
/// </summary>
public enum SentezIsDurumu
{
    /// <summary>Kuyruğa alındı, worker tarafından alınmayı bekliyor.</summary>
    Kuyrukta = 0,

    /// <summary>Worker tarafından aktif olarak işleniyor.</summary>
    Calisiyor = 1,

    /// <summary>Tüm hedeflenen bölümler başarıyla seslendirildi.</summary>
    Tamamlandi = 2,

    /// <summary>Bazı bölümler tamamlandı, bazıları hata aldı.</summary>
    KismenTamamlandi = 3,

    /// <summary>Kullanıcı tarafından iptal edildi.</summary>
    IptalEdildi = 4,

    /// <summary>Kritik veya zaman aşımı hatası nedeniyle durdu.</summary>
    Hata = 9
}
