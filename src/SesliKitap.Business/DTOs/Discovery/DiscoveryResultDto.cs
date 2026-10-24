using SesliKitap.Data.Enums;

namespace SesliKitap.Business.DTOs.Discovery;

/// <summary>
/// Kitap keşif sağlayıcısı bilgisi.
/// </summary>
/// <param name="Kod">Kısa sağlayıcı kodu (<c>ia</c>, <c>ol</c>, <c>gutenberg</c>, <c>web</c>).</param>
/// <param name="Ad">Kullanıcıya gösterilen sağlayıcı adı.</param>
/// <param name="KaynakTipi">Veritabanı kaynak tipi karşılığı.</param>
/// <param name="EtkinMi">Sağlayıcının etkin olup olmadığı.</param>
/// <param name="TelifUyarisiGerekliMi">Seçildiğinde telif uyarısı bandı gösterilip gösterilmeyeceği.</param>
public sealed record DiscoveryProviderDto(
    string Kod,
    string Ad,
    KaynakTipi KaynakTipi,
    bool EtkinMi,
    bool TelifUyarisiGerekliMi = false);

/// <summary>
/// Birleştirilmiş kitap keşif sonucunda yer alan tekil kaynak referansı.
/// </summary>
/// <param name="SaglayiciKodu">Kısa sağlayıcı kodu (<c>ia</c>, <c>ol</c>, <c>gutenberg</c>, <c>web</c>).</param>
/// <param name="KaynakTipi">Kaynak tipi.</param>
/// <param name="KaynakKimligi">Sağlayıcıdaki benzersiz kimlik.</param>
/// <param name="SayfaUrl">Sağlayıcıdaki kitap sayfası URL'si.</param>
public sealed record DiscoverySourceBadgeDto(
    string SaglayiciKodu,
    KaynakTipi KaynakTipi,
    string KaynakKimligi,
    string? SayfaUrl);

/// <summary>
/// Uygulama içi kitap keşif aramasından dönen tekilleştirilmiş ve puanlanmış kitap sonucu.
/// </summary>
public sealed record DiscoveryResultDto(
    string SaglayiciKodu,
    KaynakTipi KaynakTipi,
    string KaynakKimligi,
    string Baslik,
    string? Yazar,
    int? YayinYili,
    string Dil,
    string? KapakUrl,
    string? SayfaUrl,
    int IndirmeSayisi,
    IReadOnlyList<DosyaFormati> MevcutFormatlar,
    IReadOnlyList<DiscoveryFileDto> Dosyalar,
    IReadOnlyList<DiscoverySourceBadgeDto> Kaynaklar,
    double Puan,
    bool DogrulanmamisWebKaynagi = false,
    bool KutuphanedeVar = false,
    Guid? MevcutBookId = null);
