using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SesliKitap.Business.Options;

namespace SesliKitap.API.HealthChecks;

/// <summary>
/// Uzak GPU TTS sunucusunun (<c>Tts:HealthUrl</c>) erişilebilirliğini GPU konteyneri uyandırmadan kontrol eden sağlık denetimi.
/// </summary>
public sealed class TtsServerHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<TtsOptions> _ttsOptions;

    /// <summary>
    /// <see cref="TtsServerHealthCheck"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public TtsServerHealthCheck(HttpClient httpClient, IOptions<TtsOptions> ttsOptions)
    {
        _httpClient = httpClient;
        _ttsOptions = ttsOptions;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var healthUrl = _ttsOptions.Value.HealthUrl;
        if (string.IsNullOrWhiteSpace(healthUrl) ||
            healthUrl.Contains("<workspace>", StringComparison.OrdinalIgnoreCase))
        {
            return HealthCheckResult.Healthy("TTS sağlık URL'si yapılandırılmamış (isteğe bağlı / soğuk beklemede).");
        }

        try
        {
            using var response = await _httpClient.GetAsync(healthUrl, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("TTS sunucusu erişilebilir.");
            }

            return HealthCheckResult.Degraded($"TTS sunucusu HTTP {(int)response.StatusCode} döndürdü.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("TTS sunucusuna ulaşılamadı (sistem çalışmaya devam ediyor).", ex);
        }
    }
}
