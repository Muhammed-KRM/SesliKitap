using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;

namespace SesliKitap.Business.Services;

/// <summary>
/// Durumsuz (stateless) HMAC-SHA256 tabanlı süreli medya erişim belirteci (<c>?t=</c>) üretici ve doğrulayıcı.
/// </summary>
public sealed class MediaTokenManager : IMediaTokenService
{
    private readonly byte[] _secretBytes;
    private readonly int _lifetimeHours;
    private readonly IClock _clock;

    /// <summary>
    /// <see cref="MediaTokenManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="securityOptions">Güvenlik yapılandırma seçenekleri.</param>
    /// <param name="clock">Sistem saati soyutlaması.</param>
    public MediaTokenManager(IOptions<SecurityOptions> securityOptions, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(securityOptions);
        ArgumentNullException.ThrowIfNull(clock);

        var opts = securityOptions.Value;
        _secretBytes = Encoding.UTF8.GetBytes(opts.MediaTokenSecret);
        _lifetimeHours = opts.MediaTokenLifetimeHours > 0 ? opts.MediaTokenLifetimeHours : 6;
        _clock = clock;
    }

    /// <inheritdoc />
    public (string Token, DateTimeOffset ExpiresAt) Create()
    {
        var expiresAt = _clock.UtcNow.AddHours(_lifetimeHours);
        var expiresUnixStr = expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        var payloadPart = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(expiresUnixStr));
        var signatureBytes = ComputeSignature(expiresUnixStr);
        var signaturePart = Base64Url.EncodeToString(signatureBytes);

        return ($"{payloadPart}.{signaturePart}", expiresAt);
    }

    /// <inheritdoc />
    public bool Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var dotIndex = token.IndexOf('.', StringComparison.Ordinal);
        if (dotIndex <= 0 || dotIndex >= token.Length - 1 || token.IndexOf('.', dotIndex + 1) >= 0)
        {
            return false;
        }

        var payloadPart = token[..dotIndex];
        var signaturePart = token[(dotIndex + 1)..];

        byte[] payloadBytes;
        byte[] providedSignatureBytes;
        try
        {
            payloadBytes = Base64Url.DecodeFromChars(payloadPart);
            providedSignatureBytes = Base64Url.DecodeFromChars(signaturePart);
        }
        catch (FormatException)
        {
            return false;
        }

        var expiresUnixStr = Encoding.UTF8.GetString(payloadBytes);
        if (!long.TryParse(expiresUnixStr, NumberStyles.None, CultureInfo.InvariantCulture, out var expiresUnix))
        {
            return false;
        }

        var expectedSignatureBytes = ComputeSignature(expiresUnixStr);
        if (!CryptographicOperations.FixedTimeEquals(providedSignatureBytes, expectedSignatureBytes))
        {
            return false;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresUnix);
        return _clock.UtcNow <= expiresAt;
    }

    private byte[] ComputeSignature(string expiresUnixStr)
    {
        var messageBytes = Encoding.UTF8.GetBytes($"media:{expiresUnixStr}");
        return HMACSHA256.HashData(_secretBytes, messageBytes);
    }
}
