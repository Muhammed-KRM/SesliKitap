using System.Reflection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SesliKitap.Business.DTOs.Health;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.Business.Services;

/// <summary>
/// <see cref="HealthCheckService"/> üzerinden tüm altyapı bileşenlerinin sağlık durumunu derleyen servis.
/// </summary>
public sealed class HealthReportManager : IHealthReportService
{
    private static readonly string AppVersion =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.1.0";

    private readonly HealthCheckService _healthCheckService;
    private readonly IClock _clock;

    /// <summary>
    /// <see cref="HealthReportManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="healthCheckService">ASP.NET Core sağlık kontrolü servisi.</param>
    /// <param name="clock">Sistem saati soyutlaması.</param>
    public HealthReportManager(HealthCheckService healthCheckService, IClock clock)
    {
        _healthCheckService = healthCheckService;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<HealthReportDto> GetDetailedReportAsync(CancellationToken ct = default)
    {
        var report = await _healthCheckService.CheckHealthAsync(ct);

        var components = new Dictionary<string, HealthComponentDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, entry) in report.Entries)
        {
            if (string.Equals(key, "masstransit-bus", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var description = entry.Description ?? entry.Exception?.Message;
            components[key] = new HealthComponentDto(
                Status: entry.Status.ToString(),
                DurationMs: (long)entry.Duration.TotalMilliseconds,
                Description: description);
        }

        return new HealthReportDto(
            Status: report.Status.ToString(),
            Version: AppVersion,
            TotalDurationMs: (long)report.TotalDuration.TotalMilliseconds,
            TimestampUtc: _clock.UtcNow,
            Components: components);
    }
}
