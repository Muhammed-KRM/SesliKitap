using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SesliKitap.IntegrationTests.Fixtures;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly ContainersFixture _containers;

    public ApiFactory(ContainersFixture containers)
    {
        _containers = containers;
    }

    public HttpClient CreateAuthorizedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ContainersFixture.TestApiKey);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        var overrides = new Dictionary<string, string?>
        {
            ["Testing:DisableEnvFile"] = "true",
            ["ConnectionStrings:DefaultConnection"] = _containers.PostgresConnectionString,
            ["Redis:ConnectionString"] = _containers.RedisConnectionString,
            ["RabbitMQ:Host"] = _containers.RabbitMqHost,
            ["RabbitMQ:Port"] = _containers.RabbitMqPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RabbitMQ:Username"] = "guest",
            ["RabbitMQ:Password"] = "guest",
            ["RabbitMQ:VirtualHost"] = "/",
            ["Minio:Endpoint"] = _containers.MinioEndpoint,
            ["Minio:AccessKey"] = "minioadmin",
            ["Minio:SecretKey"] = "minioadmin123",
            ["Minio:BucketName"] = "seslikitap-inttest",
            ["Minio:UseSsl"] = "false",
            ["Security:ApiKey"] = ContainersFixture.TestApiKey,
            ["Security:MediaTokenSecret"] = ContainersFixture.TestMediaSecret
        };

        foreach (var (key, value) in overrides)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.AddMassTransitTestHarness();
        });
    }
}
