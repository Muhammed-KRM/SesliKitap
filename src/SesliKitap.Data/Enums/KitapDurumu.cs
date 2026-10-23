namespace SesliKitap.Data.Enums;

/// <summary>
/// Kitabın işleme ve seslendirme yaşam döngüsü durumları.
/// </summary>
public enum KitapDurumu
{
    /// <summary>Harici keşif kaynağından indiriliyor.</summary>
    Indiriliyor = 0,

    /// <summary>Metin çıkarma, temizleme ve bölümleme işlemi devam ediyor.</summary>
    Isleniyor = 1,

    /// <summary>Bölümler çıkarıldı; kullanıcı önizlemesi ve seslendirme başlatmayı bekliyor.</summary>
    OnizlemeBekliyor = 2,

    /// <summary>Bölümler GPU TTS motorunda seslendiriliyor.</summary>
    Seslendiriliyor = 3,

    /// <summary>Bazı bölümlerin sesi hazır; diğerleri bekliyor veya iptal edildi.</summary>
    KismenHazir = 4,

    /// <summary>Dahil edilen tüm bölümlerin sesi hazır.</summary>
    Hazir = 5,

    /// <summary>PDF taranmış görüntü tabanlı; metin katmanı için OCR gerekiyor.</summary>
    OcrGerekli = 6,

    /// <summary>İndirme, metin çıkarma veya sentez sırasında hata oluştu.</summary>
    Hata = 9
}
