using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SesliKitap.API.HealthChecks;

/// <summary>
/// RabbitMQ mesaj kuyruğu sunucusunun AMQP portuna erişilebilirliği denetleyen sağlık kontrolü.
/// </summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// <see cref="RabbitMqHealthCheck"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="configuration">Uygulama yapılandırması.</param>
    public RabbitMqHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
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

            var host = _configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
            var port = _configuration.GetValue<int?>("RabbitMQ:Port") ?? 5673;

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, port, cts.Token);
            return HealthCheckResult.Healthy($"RabbitMQ AMQP portu ({host}:{port}) erişilebilir.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ sağlık kontrolü başarısız.", ex);
        }
    }
}
