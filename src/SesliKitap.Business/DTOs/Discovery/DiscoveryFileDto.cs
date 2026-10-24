using SesliKitap.Data.Enums;

namespace SesliKitap.Business.DTOs.Discovery;

/// <summary>
/// Dış kaynaktaki indirilebilir tek bir kitap dosyası bilgisi.
/// </summary>
/// <param name="DosyaAdi">Dosya adı.</param>
/// <param name="Format">Tespit edilen dosya formatı (<see cref="DosyaFormati"/>).</param>
/// <param name="BoyutBayt">Varsa bayt cinsinden dosya boyutu.</param>
/// <param name="IndirmeUrl">Doğrudan indirme URL'si.</param>
public sealed record DiscoveryFileDto(
    string DosyaAdi,
    DosyaFormati Format,
    long? BoyutBayt,
    string IndirmeUrl);
