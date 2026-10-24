using Microsoft.Extensions.Options;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Discovery;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Helpers;
using SesliKitap.Business.Infrastructure.Discovery;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;
using SesliKitap.Data.Repositories;

namespace SesliKitap.Business.Services;

/// <summary>
/// <see cref="IDiscoveryService"/> sözleşmesinin paralel sağlayıcı sorgulama, önbellekleme, tekilleştirme ve sıralama uygulaması.
/// </summary>
public sealed class DiscoveryManager : IDiscoveryService
{
    private readonly IReadOnlyList<IBookSearchProvider> _providers;
    private readonly IBookRepository _bookRepository;
    private readonly ICacheService _cache;
    private readonly ILogService _logService;
    private readonly DiscoveryOptions _options;

    /// <summary>
    /// <see cref="DiscoveryManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public DiscoveryManager(
        IEnumerable<IBookSearchProvider> providers,
        IBookRepository bookRepository,
        ICacheService cache,
        ILogService logService,
        IOptions<DiscoveryOptions> options)
    {
        _providers = providers.ToList();
        _bookRepository = bookRepository;
        _cache = cache;
        _logService = logService;
        _options = options.Value;
    }

    /// <inheritdoc />
    public IReadOnlyList<DiscoveryProviderDto> GetProviders()
    {
        return _providers
            .Where(p => p.IsEnabled)
            .Select(p => new DiscoveryProviderDto(
                p.Code,
                p.DisplayName,
                p.SourceType,
                p.IsEnabled,
                p.RequiresCopyrightWarning))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveryResultDto>> SearchAsync(
        string query,
        IReadOnlyList<string>? providerCodes,
        int page,
        string? traceId = null,
        CancellationToken ct = default)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length is < 2 or > 200)
        {
            throw new BusinessException("Arama sorgusu 2 ile 200 karakter arasında olmalıdır.");
        }

        var safePage = Math.Max(1, page);
        var activeProviders = _providers.Where(p => p.IsEnabled).ToList();

        if (providerCodes is { Count: > 0 })
        {
            var requestedSet = new HashSet<string>(providerCodes, StringComparer.OrdinalIgnoreCase);
            activeProviders = activeProviders.Where(p => requestedSet.Contains(p.Code)).ToList();
        }
        else
        {
            // Web araması açıkça seçilmedikçe varsayılan aramaya dahil edilmez
            activeProviders = activeProviders.Where(p => !p.RequiresCopyrightWarning).ToList();
        }

        if (activeProviders.Count == 0)
        {
            return [];
        }

        var normQuery = TurkishTextHelper.NormalizeForSearch(trimmed);
        var queryHash = HashHelper.Sha1Hex($"{normQuery}|{safePage}");

        var tasks = activeProviders.Select(p => QuerySingleProviderAsync(p, trimmed, safePage, queryHash, traceId, ct));
        var providerLists = await Task.WhenAll(tasks);

        var combined = providerLists.SelectMany(x => x).ToList();
        var deduplicated = DiscoveryDeduplicator.Deduplicate(combined);
        var ranked = DiscoveryResultRanker.RankAndSort(deduplicated, trimmed);

        var existingMap = await _bookRepository.GetExistingSourceMapAsync(ct);
        if (existingMap.Count == 0)
        {
            return ranked;
        }

        return ranked
            .Select(item =>
            {
                foreach (var badge in item.Kaynaklar)
                {
                    if (existingMap.TryGetValue((badge.KaynakTipi, badge.KaynakKimligi), out var existingBookId))
                    {
                        return item with { KutuphanedeVar = true, MevcutBookId = existingBookId };
                    }
                }

                return item;
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveryFileDto>> GetFilesAsync(
        string providerCode,
        string identifier,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerCode) || string.IsNullOrWhiteSpace(identifier))
        {
            throw new BusinessException("Sağlayıcı kodu ve kaynak kimliği zorunludur.");
        }

        var provider = _providers.FirstOrDefault(p =>
            p.IsEnabled && string.Equals(p.Code, providerCode.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("Keşif sağlayıcısı", providerCode);

        var cacheKey = CacheKeys.Discovery($"{provider.Code}:files", HashHelper.Sha1Hex(identifier.Trim()));
        var cached = await _cache.GetAsync<List<DiscoveryFileDto>>(cacheKey, ct);
        if (cached is not null)
        {
            return cached;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.ProviderTimeoutSec)));

        var files = await provider.GetFilesAsync(identifier.Trim(), cts.Token);
        await _cache.SetAsync(cacheKey, files.ToList(), TimeSpan.FromHours(_options.ResultCacheHours), ct);
        return files;
    }

    private async Task<IReadOnlyList<DiscoveryResultDto>> QuerySingleProviderAsync(
        IBookSearchProvider provider,
        string query,
        int page,
        string queryHash,
        string? traceId,
        CancellationToken ct)
    {
        var cacheKey = CacheKeys.Discovery(provider.Code, queryHash);
        var cached = await _cache.GetAsync<List<DiscoveryResultDto>>(cacheKey, ct);
        if (cached is not null)
        {
            return cached;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.ProviderTimeoutSec)));

        try
        {
            var list = await provider.SearchAsync(query, page, cts.Token);
            await _cache.SetAsync(
                cacheKey,
                list.ToList(),
                TimeSpan.FromHours(Math.Max(1, _options.ResultCacheHours)),
                ct);
            return list;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            await _logService.LogFunctionWarningAsync(
                ErrorCodes.DiscoveryProviderError,
                $"Keşif sağlayıcısı ({provider.Code}) yanıt vermedi veya hata döndürdü: {ex.Message}",
                new { Provider = provider.Code, Query = query, Page = page },
                traceId,
                ct: ct);
            return [];
        }
    }
}
