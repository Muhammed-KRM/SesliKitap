using Microsoft.AspNetCore.Http;
using SesliKitap.Data.Enums;

namespace SesliKitap.Business.DTOs.Voice;

/// <summary>
/// Yeni ses profili oluşturma (<c>multipart/form-data</c>) isteği.
/// </summary>
public sealed class VoiceProfileCreateRequest
{
    /// <summary>Referans ses dosyası (WAV, MP3, M4A, OGG, FLAC, WEBM — 5–30 sn önerilir, en fazla 20 MB).</summary>
    public required IFormFile Ses { get; init; }

    /// <summary>Ses profilinin benzersiz adı.</summary>
    public required string Ad { get; init; }

    /// <summary>İsteğe bağlı açıklama.</summary>
    public string? Aciklama { get; init; }

    /// <summary>İsteğe bağlı konuşmacı cinsiyeti.</summary>
    public Cinsiyet? Cinsiyet { get; init; }

    /// <summary>Referans ses kaydında okunan metnin transkripti (Hi-Fi klonlama için önerilir).</summary>
    public string? ReferansMetin { get; init; }

    /// <summary>Ses sahibinin rızasının alındığını doğrulayan etik onay (zorunlu <c>true</c>).</summary>
    public bool KullanimOnayi { get; init; }
}

/// <summary>
/// Mevcut bir ses profilinin adını, açıklamasını ve TTS parametrelerini güncelleme isteği.
/// </summary>
public sealed record VoiceProfileUpdateRequest(
    string Ad,
    string? Aciklama,
    Cinsiyet? Cinsiyet,
    string? ReferansMetin,
    VoiceParametersDto? Parametreler);

/// <summary>
/// Ses profili için örnek sesi yeniden üretme isteği.
/// </summary>
public sealed record VoiceRegenerateSampleRequest(string? Metin = null);
