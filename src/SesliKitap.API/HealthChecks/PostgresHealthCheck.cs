using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SesliKitap.Data.Context;

namespace SesliKitap.API.HealthChecks;

/// <summary>
/// PostgreSQL veritabanı bağlantısını denetleyen sağlık kontrolü.
/// </summary>
public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    /// <summary>
    /// <see cref="PostgresHealthCheck"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="dbFactory">Veritabanı bağlamı fabrikası.</param>
    public PostgresHealthCheck(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            await using var db = await _dbFactory.CreateDbContextAsync(cts.Token);
            var canConnect = await db.Database.CanConnectAsync(cts.Token);
            return canConnect
                ? HealthCheckResult.Healthy("PostgreSQL bağlantısı aktif.")
                : HealthCheckResult.Unhealthy("PostgreSQL veritabanına bağlanılamadı.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL sağlık kontrolü başarısız.", ex);
        }
    }
}
