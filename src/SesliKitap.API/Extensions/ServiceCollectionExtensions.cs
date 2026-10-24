using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi;
using SesliKitap.API.HealthChecks;
using SesliKitap.API.Middleware;

namespace SesliKitap.API.Extensions;

/// <summary>
/// Sunum katmanı (<c>SesliKitap.API</c>) servis kayıt uzantıları.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Controller, JSON, Swagger, SignalR, Sıkıştırma, CORS, Rate Limiting ve Sağlık Kontrolü servislerini kaydeder.
    /// </summary>
    /// <param name="services">Servis koleksiyonu.</param>
    /// <param name="configuration">Uygulama yapılandırması.</param>
    /// <param name="environment">Barındırma ortamı bilgisi.</param>
    /// <returns>Güncellenmiş servis koleksiyonu.</returns>
    public static IServiceCollection AddApiPresentation(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddMemoryCache();

        // 1) Controller ve JSON Serileştirme (Enum'lar HTTP'de string, camelCase)
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        // 2) Swagger / OpenAPI (X-API-Key şeması ve XML belgeleme)
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "SesliKitap API",
                Version = "v1",
                Description = "SesliKitap — Yapay Zeka Destekli Türkçe Sesli Kitap Üretim ve Dinleme Platformu API"
            });

            options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = ApiKeyAuthMiddleware.ApiKeyHeaderName,
                In = ParameterLocation.Header,
                Description = "Yönetici/istemci erişim anahtarı (X-API-Key başlığı)."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("ApiKey", document)] = []
            });

            foreach (var xmlFile in Directory.GetFiles(AppContext.BaseDirectory, "SesliKitap.*.xml"))
            {
                options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);
            }
        });

        // 3) SignalR Canlı Bildirim
        services.AddSignalR(options =>
            {
                options.MaximumReceiveMessageSize = 64 * 1024;
                options.EnableDetailedErrors = environment.IsDevelopment();
            })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        // 4) Yanıt Sıkıştırma (Brotli + Gzip; ses akışları hariç)
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/problem+json"]);
        });

        // 5) Multipart / Form Yükleme Limitleri (Azami 300 MB + 1 MB)
        var maxUploadBytes = configuration.GetValue<long?>("Upload:MaxBookBytes") ?? 314_572_800L;
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = maxUploadBytes + (1024 * 1024);
        });

        // 6) CORS Politikası
        var allowedOrigins = configuration.GetSection("Security:AllowedOrigins").Get<string[]>()
                             ?? ["http://localhost:4300"];
        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithExposedHeaders("Content-Range", "Accept-Ranges", "Content-Length", "Content-Disposition");
            });
        });

        // 7) Rate Limiter (genel, yükleme, keşif)
        var generalLimit = configuration.GetValue<int?>("RateLimiting:GeneralPerMinute") ?? 300;
        var uploadLimit = configuration.GetValue<int?>("RateLimiting:UploadPerMinute") ?? 10;
        var discoveryLimit = configuration.GetValue<int?>("RateLimiting:DiscoveryPerMinute") ?? 30;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.Headers.RetryAfter = "60";
                ctx.HttpContext.Response.ContentType = "application/problem+json; charset=utf-8";
                await ctx.HttpContext.Response.WriteAsJsonAsync(new
                {
                    status = 429,
                    title = "İstek Limiti Aşıldı",
                    detail = string.Create(CultureInfo.InvariantCulture, $"Dakikalık istek sınırı aşıldı. Lütfen biraz bekleyin."),
                    traceId = ctx.HttpContext.TraceIdentifier
                }, ct);
            };

            options.AddPolicy("general", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "global",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = generalLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy("upload", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "global",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = uploadLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy("discovery", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "global",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = discoveryLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        // 8) Altyapı Sağlık Kontrolleri
        services.AddHttpClient<TtsServerHealthCheck>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres")
            .AddCheck<RedisHealthCheck>("redis")
            .AddCheck<RabbitMqHealthCheck>("rabbitmq")
            .AddCheck<MinioHealthCheck>("minio")
            .AddCheck<TtsServerHealthCheck>(
                "tts",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded);

        return services;
    }
}
