using Microsoft.Extensions.Diagnostics.HealthChecks;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.API.HealthChecks;

/// <summary>
/// MinIO nesne deposu ve varsayılan bucket erişilebilirliğini denetleyen sağlık kontrolü.
/// </summary>
public sealed class MinioHealthCheck : IHealthCheck
{
    private readonly IFileStorageService _fileStorageService;

    /// <summary>
    /// <see cref="MinioHealthCheck"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="fileStorageService">Dosya depolama servisi.</param>
    public MinioHealthCheck(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
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

            var exists = await _fileStorageService.BucketExistsAsync(cts.Token);
            return exists
                ? HealthCheckResult.Healthy("MinIO bucket erişilebilir.")
                : HealthCheckResult.Unhealthy("MinIO bucket bulunamadı veya erişilemiyor.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MinIO bağlantısı başarısız.", ex);
        }
    }
}
