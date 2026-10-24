using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SesliKitap.Business.DTOs.Discovery;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Uygulama içi Türkçe kitap keşfi (<c>/api/discovery</c>) uç noktaları.
/// </summary>
[Route("api/discovery")]
[EnableRateLimiting("discovery")]
public sealed class DiscoveryController : BaseApiController
{
    private readonly IDiscoveryService _discoveryService;

    /// <summary>
    /// <see cref="DiscoveryController"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public DiscoveryController(IDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    /// <summary>
    /// Etkin kitap arama sağlayıcılarını listeler.
    /// </summary>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(IReadOnlyList<DiscoveryProviderDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DiscoveryProviderDto>> GetProviders()
    {
        return Ok(_discoveryService.GetProviders());
    }

    /// <summary>
    /// Seçili sağlayıcılarda paralel Türkçe kitap araması yapar, sonuçları tekilleştirir ve puanlayarak sıralar.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<DiscoveryResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<DiscoveryResultDto>>> Search(
        [FromQuery] string q,
        [FromQuery] string? kaynaklar = null,
        [FromQuery] int sayfa = 1,
        CancellationToken ct = default)
    {
        IReadOnlyList<string>? providerList = null;
        if (!string.IsNullOrWhiteSpace(kaynaklar))
        {
            providerList = kaynaklar
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        var results = await _discoveryService.SearchAsync(q, providerList, sayfa, TraceId, ct);
        return Ok(results);
    }

    /// <summary>
    /// Belirtilen sağlayıcı ve kaynak kimliği için indirilebilir dosyaları (format, boyut, URL) listeler.
    /// </summary>
    [HttpGet("{provider}/{kimlik}/files")]
    [ProducesResponseType(typeof(IReadOnlyList<DiscoveryFileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DiscoveryFileDto>>> GetFiles(
        string provider,
        string kimlik,
        CancellationToken ct = default)
    {
        var files = await _discoveryService.GetFilesAsync(provider, kimlik, ct);
        return Ok(files);
    }
}
