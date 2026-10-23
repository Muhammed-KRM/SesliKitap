namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Tarayıcı <c>&lt;audio&gt;</c> ve <c>&lt;img&gt;</c> etiketlerinin güvenli medya erişimi için
/// süreli HMAC-SHA256 belirteci (token) üreten ve doğrulayan servis sözleşmesi.
/// </summary>
public interface IMediaTokenService
{
    /// <summary>
    /// Yeni bir süreli medya erişim belirteci üretir.
    /// </summary>
    /// <returns>Üretilen belirteç ve UTC son geçerlilik zamanı.</returns>
    (string Token, DateTimeOffset ExpiresAt) Create();

    /// <summary>
    /// Verilen medya belirtecinin imzasını ve geçerlilik süresini sabit zamanlı olarak doğrular.
    /// </summary>
    /// <param name="token">Sorgu parametresinden (<c>?t=</c>) gelen belirteç.</param>
    /// <returns>Geçerliyse <c>true</c>, aksi halde <c>false</c>.</returns>
    bool Validate(string? token);
}
