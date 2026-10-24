using SesliKitap.Business.Constants;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;

namespace SesliKitap.Business.Services;

/// <summary>
/// <see cref="IChapterAudioService"/> sözleşmesinin MinIO + Redis önbellekli uygulaması (Bölüm 4.15.4).
/// </summary>
public sealed class ChapterAudioManager : IChapterAudioService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private readonly IChapterRepository _chapterRepository;
    private readonly IFileStorageService _storage;
    private readonly ICacheService _cache;

    /// <summary>
    /// <see cref="ChapterAudioManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ChapterAudioManager(
        IChapterRepository chapterRepository,
        IFileStorageService storage,
        ICacheService cache)
    {
        ArgumentNullException.ThrowIfNull(chapterRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(cache);

        _chapterRepository = chapterRepository;
        _storage = storage;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<ChapterAudioInfo> GetAudioInfoAsync(
        Guid chapterId,
        string format = "opus",
        CancellationToken ct = default)
    {
        var normalizedFormat = string.IsNullOrWhiteSpace(format)
            ? "opus"
            : format.Trim().ToLowerInvariant();

        if (normalizedFormat is not ("opus" or "mp3"))
        {
            throw new BusinessException("Desteklenmeyen ses formatı. 'opus' veya 'mp3' kullanın.");
        }

        var cacheKey = CacheKeys.ChapterAudioInfo(chapterId, normalizedFormat);
        var cached = await _cache.GetAsync<ChapterAudioInfo>(cacheKey, ct);
        if (cached is not null)
        {
            return cached;
        }

        var chapter = await _chapterRepository.GetByIdAsync(chapterId, ct)
            ?? throw new NotFoundException("Bölüm", chapterId);

        if (chapter.SesDurumu is not (BolumSesDurumu.Hazir or BolumSesDurumu.GuncelDegil))
        {
            throw new NotFoundException("Bölüm sesi", chapterId);
        }

        var objectKey = normalizedFormat == "mp3" ? chapter.Mp3Yolu : chapter.OpusYolu;
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new NotFoundException("Bölüm ses dosyası", chapterId);
        }

        var storageInfo = await _storage.GetInfoAsync(objectKey, ct)
            ?? throw new NotFoundException("Bölüm ses dosyası", chapterId);

        var contentType = normalizedFormat == "mp3" ? "audio/mpeg" : "audio/ogg";
        var rawEtag = string.IsNullOrWhiteSpace(storageInfo.ETag)
            ? $"{chapterId:N}-{chapter.SesMetinSurumu ?? chapter.MetinSurumu}-{normalizedFormat}"
            : storageInfo.ETag.Trim('"');
        var etag = $"\"{rawEtag}\"";

        var info = new ChapterAudioInfo(objectKey, storageInfo.Size, etag, contentType);
        await _cache.SetAsync(cacheKey, info, CacheTtl, ct);
        return info;
    }

    /// <inheritdoc />
    public async Task CopyRangeToAsync(
        string objectKey,
        long offset,
        long length,
        Stream destination,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentNullException.ThrowIfNull(destination);

        await _storage.CopyToAsync(objectKey, destination, offset, length, ct);
    }

    /// <inheritdoc />
    public async Task<string> GetTimestampsJsonAsync(Guid chapterId, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.ChapterTimestamps(chapterId);
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        var chapter = await _chapterRepository.GetByIdAsync(chapterId, ct)
            ?? throw new NotFoundException("Bölüm", chapterId);

        if (chapter.SesDurumu is not (BolumSesDurumu.Hazir or BolumSesDurumu.GuncelDegil) ||
            string.IsNullOrWhiteSpace(chapter.ZamanDamgasiYolu))
        {
            throw new NotFoundException("Bölüm zaman damgaları", chapterId);
        }

        var json = await _storage.ReadTextAsync(chapter.ZamanDamgasiYolu, ct)
            ?? throw new NotFoundException("Bölüm zaman damgaları dosyası", chapterId);

        await _cache.SetStringAsync(cacheKey, json, CacheTtl, ct);
        return json;
    }
}
