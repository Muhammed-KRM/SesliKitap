using System.Text.Json;
using Microsoft.Extensions.Logging;
using SesliKitap.Business.Interfaces;
using StackExchange.Redis;

namespace SesliKitap.Business.Infrastructure.Cache;

/// <summary>
/// <see cref="IConnectionMultiplexer"/> tabanlı, hata-açık (fail-open) Redis önbellek ve kilit servisi.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger<RedisCacheService> _logger;

    /// <summary>
    /// <see cref="RedisCacheService"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="multiplexer">Redis bağlantı çoklayıcısı.</param>
    /// <param name="logger">Yapılandırılmış günlükleyici.</param>
    public RedisCacheService(IConnectionMultiplexer multiplexer, ILogger<RedisCacheService> logger)
    {
        _multiplexer = multiplexer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            if (!_multiplexer.IsConnected)
            {
                return default;
            }

            var db = _multiplexer.GetDatabase();
            var raw = await db.StringGetAsync(key);
            if (!raw.HasValue || raw.IsNullOrEmpty)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>((string)raw!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis okuma hatası (fail-open): {Key}", key);
            return default;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            if (!_multiplexer.IsConnected)
            {
                return;
            }

            var db = _multiplexer.GetDatabase();
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await db.StringSetAsync(key, json, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis yazma hatası (fail-open): {Key}", key);
        }
    }

    /// <inheritdoc />
    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
        {
            return cached;
        }

        var value = await factory(ct);
        if (value is not null)
        {
            await SetAsync(key, value, ttl, ct);
        }

        return value;
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            if (!_multiplexer.IsConnected)
            {
                return;
            }

            var db = _multiplexer.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis silme hatası (fail-open): {Key}", key);
        }
    }

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        try
        {
            if (!_multiplexer.IsConnected)
            {
                return;
            }

            var db = _multiplexer.GetDatabase();
            var endpoints = _multiplexer.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                var server = _multiplexer.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica)
                {
                    continue;
                }

                var keysToDelete = new List<RedisKey>();
                await foreach (var key in server.KeysAsync(pattern: $"{prefix}*", pageSize: 500).WithCancellation(ct))
                {
                    keysToDelete.Add(key);
                    if (keysToDelete.Count >= 500)
                    {
                        await db.KeyDeleteAsync(keysToDelete.ToArray());
                        keysToDelete.Clear();
                    }
                }

                if (keysToDelete.Count > 0)
                {
                    await db.KeyDeleteAsync(keysToDelete.ToArray());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis önek silme hatası (fail-open): {Prefix}", prefix);
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryAcquireLockAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            if (!_multiplexer.IsConnected)
            {
                return false;
            }

            var db = _multiplexer.GetDatabase();
            return await db.StringSetAsync(key, value, ttl, When.NotExists);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis kilit alma hatası (fail-open): {Key}", key);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetStringAsync(string key, CancellationToken ct = default)
    {
        try
        {
            if (!_multiplexer.IsConnected)
            {
                return null;
            }

            var db = _multiplexer.GetDatabase();
            var val = await db.StringGetAsync(key);
            return val.HasValue ? (string?)val : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis metin okuma hatası (fail-open): {Key}", key);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetStringAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            if (!_multiplexer.IsConnected)
            {
                return;
            }

            var db = _multiplexer.GetDatabase();
            await db.StringSetAsync(key, value, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis metin yazma hatası (fail-open): {Key}", key);
        }
    }
}
