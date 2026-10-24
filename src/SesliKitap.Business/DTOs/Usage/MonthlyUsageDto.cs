namespace SesliKitap.Business.DTOs.Usage;

/// <summary>
/// Aylık GPU kullanım geçmişi kalemi (son 6 ay grafiği için).
/// </summary>
public sealed record MonthlyUsageHistoryItemDto(
    int Yil,
    int Ay,
    string DonemEtiketi,
    long ToplamGpuMs,
    decimal ToplamMaliyetUsd,
    double UretilenSesSaat,
    int TamamlananIsSayisi);

/// <summary>
/// Seçilen ay ve son 6 aylık GPU/TTS kullanım ve bütçe raporu (Faz 8).
/// </summary>
public sealed record MonthlyUsageDto(
    int Yil,
    int Ay,
    long ToplamGpuMs,
    double ToplamGpuSaat,
    decimal ToplamMaliyetUsd,
    decimal AylikButceUsd,
    double ButceKullanimYuzdesi,
    double UretilenSesSn,
    double UretilenSesSaat,
    int TamamlananIsSayisi,
    int SeslendirilenKitapSayisi,
    long OnbellektenParcaSayisi,
    long UretilenParcaSayisi,
    IReadOnlyList<MonthlyUsageHistoryItemDto> SonAltiAy);
