namespace SesliKitap.Data.Enums;

/// <summary>
/// Kitap işleme, ses profili hazırlama ve seslendirme (TTS) pipeline aşamaları.
/// </summary>
public enum PipelineAsamasi
{
    /// <summary>Harici kaynaktan kitap dosyasının indirilmesi.</summary>
    Indirme = 0,

    /// <summary>PDF, EPUB veya TXT dosyasından ham metin ve blokların çıkarılması.</summary>
    MetinCikarma = 1,

    /// <summary>Üst/alt bilgi, sayfa numarası, dipnot ve tireli satır sonu temizliği.</summary>
    Temizleme = 2,

    /// <summary>TOC, başlık puntoları veya sezgisel kurallarla bölümlerin ayrıştırılması.</summary>
    BolumTespiti = 3,

    /// <summary>Kitap kapak görselinin çıkarılması ve optimize edilmesi.</summary>
    KapakUretimi = 4,

    /// <summary>Referans ses kaydının kırpılması, normalize edilmesi ve örnek ses üretimi.</summary>
    SesProfiliHazirlama = 5,

    /// <summary>Bölüm metninin kısaltma duyarlı olarak cümlelere ayrılması.</summary>
    Segmentasyon = 6,

    /// <summary>Sayı, tarih, saat, para birimi ve Romen rakamlarının Türkçe okunuşa çevrilmesi.</summary>
    Normalizasyon = 7,

    /// <summary>Parçaların sunucusuz GPU TTS motorunda seslendirilmesi.</summary>
    Sentez = 8,

    /// <summary>Parça seslerinin doğal sessizliklerle birleştirilmesi ve zaman damgası hesabı.</summary>
    SesBirlestirme = 9,

    /// <summary>EBU R128 ses yüksekliği normalizasyonu ve Opus/MP3/M4B kodlama.</summary>
    Kodlama = 10,

    /// <summary>Üretilen medya ve meta veri dosyalarının MinIO nesne deposuna yüklenmesi.</summary>
    Yukleme = 11,

    /// <summary>Pipeline akışının başarıyla tamamlanması.</summary>
    Tamamlandi = 12
}
