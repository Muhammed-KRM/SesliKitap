using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace SesliKitap.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<ContainersFixture>
{
    public const string Name = "IntegrationContainers";
}

public sealed class ContainersFixture : IAsyncLifetime
{
    public const string TestApiKey = "test-admin-api-key-minimum-32-characters-long-12345";
    public const string TestMediaSecret = "test-media-token-secret-minimum-32-chars-long-67890";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("seslikitap_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private readonly MinioContainer _minio = new MinioBuilder()
        .WithImage("elestio/minio:latest")
        .WithUsername("minioadmin")
        .WithPassword("minioadmin123")
        .Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();

    public string RedisConnectionString => _redis.GetConnectionString();

    public string RabbitMqHost => _rabbitMq.Hostname;

    public int RabbitMqPort => _rabbitMq.GetMappedPublicPort(5672);

    public string MinioEndpoint => $"{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _redis.StartAsync(),
            _rabbitMq.StartAsync(),
            _minio.StartAsync());
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(),
            _rabbitMq.DisposeAsync().AsTask(),
            _minio.DisposeAsync().AsTask());
    }
}
