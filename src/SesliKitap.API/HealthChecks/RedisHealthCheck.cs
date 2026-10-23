using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace SesliKitap.API.HealthChecks;

/// <summary>
/// Redis önbellek sunucusu bağlantısını <c>PING</c> komutuyla denetleyen sağlık kontrolü.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _multiplexer;

    /// <summary>
    /// <see cref="RedisHealthCheck"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="multiplexer">Redis bağlantı çoklayıcısı.</param>
    public RedisHealthCheck(IConnectionMultiplexer multiplexer)
    {
        _multiplexer = multiplexer;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_multiplexer.IsConnected)
            {
                return HealthCheckResult.Unhealthy("Redis bağlantısı kapalı (fail-open modunda çalışılıyor).");
            }

            var db = _multiplexer.GetDatabase();
            var ping = await db.PingAsync();
            return HealthCheckResult.Healthy($"Redis PING yanıt süresi: {ping.TotalMilliseconds:F1} ms.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis sağlık kontrolü başarısız.", ex);
        }
    }
}
