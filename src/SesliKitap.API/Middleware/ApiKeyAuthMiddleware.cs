using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;

namespace SesliKitap.API.Middleware;

/// <summary>
/// <c>X-API-Key</c> başlığı, SignalR <c>access_token</c> parametresi ve medya akışı <c>?t=</c>
/// belirtecini sabit zamanlı olarak doğrulayan ve kaba kuvvet (brute-force) koruması sağlayan ara katman.
/// </summary>
public sealed class ApiKeyAuthMiddleware
{
    /// <summary>Kimliği doğrulanmış istekleri işaretlemek için <see cref="HttpContext.Items"/> anahtarı.</summary>
    public const string AuthenticatedItemKey = "SesliKitap.ApiKeyAuthenticated";

    /// <summary>API anahtarı HTTP başlık adı.</summary>
    public const string ApiKeyHeaderName = "X-API-Key";

    private const int MaxFailedAttemptsPerMinute = 20;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly IOptions<SecurityOptions> _securityOptions;
    private readonly IMediaTokenService _mediaTokenService;
    private readonly IMemoryCache _memoryCache;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    /// <summary>
    /// <see cref="ApiKeyAuthMiddleware"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        IOptions<SecurityOptions> securityOptions,
        IMediaTokenService mediaTokenService,
        IMemoryCache memoryCache,
        IHostEnvironment env,
        ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _securityOptions = securityOptions;
        _mediaTokenService = mediaTokenService;
        _memoryCache = memoryCache;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// İsteğin kimlik doğrulama kurallarını denetler.
    /// </summary>
    /// <param name="context">HTTP bağlamı.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;

        if (_env.IsDevelopment() && path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var lockoutKey = $"auth:lockout:{ipAddress}";
        if (_memoryCache.TryGetValue(lockoutKey, out _))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status429TooManyRequests,
                "Çok Fazla Başarısız Deneme",
                "Çok fazla hatalı erişim anahtarı denemesi yapıldı. Lütfen 15 dakika sonra tekrar deneyin.");
            return;
        }

        var expectedApiKey = _securityOptions.Value.ApiKey;
        var hasValidHeaderKey = context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var headerValues) &&
                                ConstantTimeEquals(headerValues.ToString(), expectedApiKey);

        if (hasValidHeaderKey)
        {
            context.Items[AuthenticatedItemKey] = true;
            await _next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        var allowsAnonymous = endpoint?.Metadata.GetMetadata<AllowAnonymousApiKeyAttribute>() is not null;
        if (allowsAnonymous)
        {
            // Eğer anonim uç noktaya geçersiz bir X-API-Key başlığı açıkça gönderildiyse başarısız say
            if (context.Request.Headers.ContainsKey(ApiKeyHeaderName))
            {
                RecordFailedAttempt(ipAddress);
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "Yetkisiz Erişim",
                    "Geçersiz X-API-Key başlığı.");
                return;
            }

            context.Items[AuthenticatedItemKey] = false;
            await _next(context);
            return;
        }

        // SignalR WebSocket bağlantıları: /hubs/... ?access_token=...
        if (path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase) &&
            context.Request.Query.TryGetValue("access_token", out var accessTokenValues) &&
            ConstantTimeEquals(accessTokenValues.ToString(), expectedApiKey))
        {
            context.Items[AuthenticatedItemKey] = true;
            await _next(context);
            return;
        }

        // Medya akışı yolları: /api/chapters/{id}/audio, /api/books/{id}/cover, /api/voice-profiles/{id}/sample
        if (IsMediaPath(path) &&
            context.Request.Query.TryGetValue("t", out var mediaTokenValues) &&
            _mediaTokenService.Validate(mediaTokenValues.ToString()))
        {
            context.Items[AuthenticatedItemKey] = true;
            await _next(context);
            return;
        }

        RecordFailedAttempt(ipAddress);
        _logger.LogWarning("Yetkisiz erişim denemesi engellendi: IP={IpAddress}, Method={Method}, Path={Path}",
            ipAddress, context.Request.Method, path);

        await WriteProblemAsync(
            context,
            StatusCodes.Status401Unauthorized,
            "Yetkisiz Erişim",
            "Geçerli bir X-API-Key başlığı veya medya erişim belirteci gereklidir.");
    }

    private void RecordFailedAttempt(string ipAddress)
    {
        var countKey = $"auth:fail:{ipAddress}";
        var attempts = _memoryCache.GetOrCreate(countKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return 0;
        });

        attempts++;
        _memoryCache.Set(countKey, attempts, TimeSpan.FromMinutes(1));

        if (attempts > MaxFailedAttemptsPerMinute)
        {
            _memoryCache.Set($"auth:lockout:{ipAddress}", true, LockoutDuration);
            _logger.LogWarning("IP adresi çok fazla başarısız kimlik doğrulama nedeniyle 15 dk kilitlendi: {IpAddress}", ipAddress);
        }
    }

    private static bool IsMediaPath(string path)
    {
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return (path.StartsWith("/api/chapters/", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith("/audio", StringComparison.OrdinalIgnoreCase))
               || (path.StartsWith("/api/books/", StringComparison.OrdinalIgnoreCase) &&
                   (path.EndsWith("/cover", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("/export", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("/source", StringComparison.OrdinalIgnoreCase) ||
                    path.Contains("/export/", StringComparison.OrdinalIgnoreCase)))
               || (path.StartsWith("/api/voice-profiles/", StringComparison.OrdinalIgnoreCase) &&
                   path.EndsWith("/sample", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ConstantTimeEquals(string? provided, string? expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));

        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash)
               && provided.Length == expected.Length;
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
