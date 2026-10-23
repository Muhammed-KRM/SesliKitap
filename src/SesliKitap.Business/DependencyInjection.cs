using System.Reflection;
using FluentValidation;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using SesliKitap.Business.Infrastructure.Cache;
using SesliKitap.Business.Infrastructure.Storage;
using SesliKitap.Business.Infrastructure.Time;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;
using SesliKitap.Business.Services;
using StackExchange.Redis;

namespace SesliKitap.Business;

/// <summary>
/// İş katmanı (<c>SesliKitap.Business</c>) bağımlılık enjeksiyonu (DI) kayıt uzantıları.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// İş katmanı servislerini, doğrulayıcıları, MinIO, Redis ve MassTransit (RabbitMQ) bileşenlerini kaydeder.
    /// </summary>
    /// <param name="services">Servis koleksiyonu.</param>
    /// <param name="configuration">Uygulama yapılandırması.</param>
    /// <param name="configureConsumers">İsteğe bağlı MassTransit consumer kayıt eylemi.</param>
    /// <returns>Güncellenmiş servis koleksiyonu.</returns>
    public static IServiceCollection AddBusinessLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 1) Tip güvenli Options kayıtları (başlangıçta zorunlu doğrulama)
        services.AddOptions<SecurityOptions>()
            .Bind(configuration.GetSection(SecurityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CacheOptions>()
            .Bind(configuration.GetSection(CacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // 2) FluentValidation doğrulayıcıları
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // 3) Zaman soyutlaması ve medya token servisi
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IMediaTokenService, MediaTokenManager>();

        // 4) MinIO nesne depolama
        services.AddSingleton<IMinioClient>(sp =>
        {
            var storageOpts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return new MinioClient()
                .WithEndpoint(storageOpts.Endpoint)
                .WithCredentials(storageOpts.AccessKey, storageOpts.SecretKey)
                .WithSSL(storageOpts.UseSsl)
                .Build();
        });
        services.AddSingleton<IFileStorageService, MinioFileStorageService>();

        // 5) Redis önbellek (AbortOnConnectFail=false ile fail-open)
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConn = configuration.GetValue<string>("Redis:ConnectionString")
                            ?? configuration.GetValue<string>("REDIS_CONNECTION_STRING")
                            ?? "localhost:6380";
            var redisOpts = ConfigurationOptions.Parse(redisConn);
            redisOpts.AbortOnConnectFail = false;
            redisOpts.ConnectTimeout = 2000;
            redisOpts.SyncTimeout = 2000;
            redisOpts.AsyncTimeout = 2000;
            return ConnectionMultiplexer.Connect(redisOpts);
        });
        services.AddSingleton<ICacheService, RedisCacheService>();

        // 6) İş servisleri
        services.AddScoped<ILogService, LogManager>();
        services.AddScoped<IHealthReportService, HealthReportManager>();

        // 7) MassTransit + RabbitMQ (Python uyumu için Raw JSON)
        services.AddMassTransit(x =>
        {
            x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("sk", false));
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((ctx, rmq) =>
            {
                var host = configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
                var port = configuration.GetValue<ushort?>("RabbitMQ:Port") ?? 5673;
                var user = configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
                var pass = configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";

                rmq.Host(host, port, "/", h =>
                {
                    h.Username(user);
                    h.Password(pass);
                });

                rmq.UseRawJsonSerializer();
                rmq.UseRawJsonDeserializer();

                rmq.UseMessageRetry(r => r.Exponential(
                    retryLimit: 3,
                    minInterval: TimeSpan.FromSeconds(1),
                    maxInterval: TimeSpan.FromSeconds(10),
                    intervalDelta: TimeSpan.FromSeconds(2)));

                rmq.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }
}
