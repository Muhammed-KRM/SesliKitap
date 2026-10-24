namespace SesliKitap.Business.DTOs.Synthesis;

/// <summary>
/// Sentez işi başlatılmadan önce hesaplanan süre, GPU ve bütçe tahmini görünüm modeli (Bölüm 4.15.4).
/// </summary>
/// <param name="BolumSayisi">Seslendirilecek hedef bölüm sayısı.</param>
/// <param name="KelimeSayisi">Hedef bölümlerin toplam kelime sayısı.</param>
/// <param name="TahminiSesSn">Tahmini üretilecek ses süresi (saniye).</param>
/// <param name="TahminiGpuSn">Tahmini GPU çalışma süresi (saniye, soğuk başlatma ve ek yük dahil).</param>
/// <param name="TahminiMaliyetUsd">Tahmini iş maliyeti (USD).</param>
/// <param name="BuAyHarcananUsd">İçinde bulunulan ayda harcanmış toplam maliyet (USD).</param>
/// <param name="AylikButceUsd">Tanımlı aylık GPU bütçe sınırı (USD).</param>
/// <param name="KalanButceUsd">Kalan aylık bütçe (USD).</param>
/// <param name="ButceAsiliyor">Bu işle birlikte aylık bütçenin aşılıp aşılmadığı.</param>
/// <param name="OnbellektekiTahminiParcaOrani">Önbellekten karşılanması beklenen tahmini parça yüzdesi (0–100).</param>
public sealed record SynthesisEstimateDto(
    int BolumSayisi,
    int KelimeSayisi,
    double TahminiSesSn,
    double TahminiGpuSn,
    decimal TahminiMaliyetUsd,
    decimal BuAyHarcananUsd,
    decimal AylikButceUsd,
    decimal KalanButceUsd,
    bool ButceAsiliyor,
    int OnbellektekiTahminiParcaOrani);
