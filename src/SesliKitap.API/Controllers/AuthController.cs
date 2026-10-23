using Microsoft.AspNetCore.Mvc;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Medya akışı (<c>&lt;audio&gt;</c> / <c>&lt;img&gt;</c>) erişim belirteci yanıt modeli.
/// </summary>
/// <param name="Token">İmzalı HMAC-SHA256 medya belirteci.</param>
/// <param name="SonGecerlilik">Belirtecin UTC son geçerlilik zamanı.</param>
public sealed record MediaTokenResponseDto(string Token, DateTimeOffset SonGecerlilik);

/// <summary>
/// Erişim anahtarı doğrulama ve süreli medya belirteci üretme uç noktaları.
/// </summary>
[Route("api/auth")]
public sealed class AuthController : BaseApiController
{
    private readonly IMediaTokenService _mediaTokenService;

    /// <summary>
    /// <see cref="AuthController"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="mediaTokenService">Medya belirteci servisi.</param>
    public AuthController(IMediaTokenService mediaTokenService)
    {
        _mediaTokenService = mediaTokenService;
    }

    /// <summary>
    /// İstemcinin gönderdiği <c>X-API-Key</c> başlığının geçerli olduğunu doğrular.
    /// </summary>
    /// <returns>Anahtar geçerliyse <c>204 No Content</c>.</returns>
    [HttpGet("verify")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Verify()
    {
        return NoContent();
    }

    /// <summary>
    /// Tarayıcı medya etiketleri (<c>?t=</c>) için süreli HMAC-SHA256 erişim belirteci üretir.
    /// </summary>
    /// <returns>Üretilen medya belirteci ve son geçerlilik zamanı.</returns>
    [HttpGet("media-token")]
    [ProducesResponseType(typeof(MediaTokenResponseDto), StatusCodes.Status200OK)]
    public ActionResult<MediaTokenResponseDto> CreateMediaToken()
    {
        var (token, expiresAt) = _mediaTokenService.Create();
        return Ok(new MediaTokenResponseDto(token, expiresAt));
    }
}
