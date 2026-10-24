using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;

namespace SesliKitap.Business.Infrastructure.Storage;

/// <summary>
/// <see cref="IMinioClient"/> tabanlı, HTTP Range akışı destekli MinIO S3 nesne depolama servisi.
/// </summary>
public sealed class MinioFileStorageService : IFileStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioFileStorageService> _logger;
    private readonly string _bucketName;

    /// <summary>
    /// <see cref="MinioFileStorageService"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="minioClient">MinIO istemcisi.</param>
    /// <param name="options">MinIO depolama yapılandırma seçenekleri.</param>
    /// <param name="logger">Yapılandırılmış günlükleyici.</param>
    public MinioFileStorageService(
        IMinioClient minioClient,
        IOptions<StorageOptions> options,
        ILogger<MinioFileStorageService> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
        _bucketName = options.Value.BucketName;
    }

    /// <inheritdoc />
    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        try
        {
            var existsArgs = new BucketExistsArgs().WithBucket(_bucketName);
            var exists = await _minioClient.BucketExistsAsync(existsArgs, ct);
            if (!exists)
            {
                var makeArgs = new MakeBucketArgs().WithBucket(_bucketName);
                await _minioClient.MakeBucketAsync(makeArgs, ct);
                _logger.LogInformation("MinIO bucket oluşturuldu: {BucketName}", _bucketName);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MinIO bucket kontrolü/oluşturulması başarısız: {BucketName}", _bucketName);
            throw new ExternalServiceException("MinIO", $"Bucket hazırlanamadı ({_bucketName}).", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> BucketExistsAsync(CancellationToken ct = default)
    {
        try
        {
            var existsArgs = new BucketExistsArgs().WithBucket(_bucketName);
            return await _minioClient.BucketExistsAsync(existsArgs, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MinIO sağlık kontrolü başarısız: {BucketName}", _bucketName);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task UploadAsync(
        string key,
        Stream content,
        long size,
        string contentType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            var putArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(key)
                .WithStreamData(content)
                .WithObjectSize(size)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putArgs, ct);
            _logger.LogDebug("MinIO nesne yüklendi: {Key} ({Size} bayt)", key, size);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MinIO nesne yükleme hatası: {Key}", key);
            throw new ExternalServiceException("MinIO", $"Nesne yüklenemedi: {key}", ex);
        }
    }

    /// <inheritdoc />
    public async Task UploadJsonAsync<T>(string key, T value, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        using var ms = new MemoryStream(bytes, writable: false);
        await UploadAsync(key, ms, bytes.Length, "application/json; charset=utf-8", ct);
    }

    /// <inheritdoc />
    public async Task<T?> ReadJsonAsync<T>(string key, CancellationToken ct = default)
    {
        var text = await ReadTextAsync(key, ct);
        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(text, JsonOptions);
    }

    /// <inheritdoc />
    public async Task<string?> ReadTextAsync(string key, CancellationToken ct = default)
    {
        try
        {
            using var ms = new MemoryStream();
            var getArgs = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(key)
                .WithCallbackStream(async (stream, token) =>
                {
                    await stream.CopyToAsync(ms, token);
                });

            await _minioClient.GetObjectAsync(getArgs, ct);
            return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MinIO metin okuma hatası: {Key}", key);
            throw new ExternalServiceException("MinIO", $"Nesne okunamadı: {key}", ex);
        }
    }

    /// <inheritdoc />
    public async Task CopyToAsync(
        string key,
        Stream destination,
        long offset = 0,
        long? length = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(destination);

        try
        {
            var getArgs = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(key)
                .WithCallbackStream(async (stream, token) =>
                {
                    if (offset <= 0 && !length.HasValue)
                    {
                        await stream.CopyToAsync(destination, 81920, token);
                        return;
                    }

                    var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
                    try
                    {
                        var remainingSkip = offset;
                        while (remainingSkip > 0)
                        {
                            var toRead = (int)Math.Min(buffer.Length, remainingSkip);
                            var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), token);
                            if (read <= 0)
                            {
                                return;
                            }

                            remainingSkip -= read;
                        }

                        var remainingCopy = length ?? long.MaxValue;
                        while (remainingCopy > 0)
                        {
                            var toRead = (int)Math.Min(buffer.Length, remainingCopy);
                            var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), token);
                            if (read <= 0)
                            {
                                break;
                            }

                            await destination.WriteAsync(buffer.AsMemory(0, read), token);
                            remainingCopy -= read;
                        }
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                    }
                });

            await _minioClient.GetObjectAsync(getArgs, ct);
        }
        catch (ObjectNotFoundException)
        {
            throw new NotFoundException("Medya Dosyası", key);
        }
        catch (NotFoundException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MinIO akış kopyalama hatası: {Key} (Offset={Offset}, Length={Length})", key, offset, length);
            throw new ExternalServiceException("MinIO", $"Medya akışı okunamadı: {key} ({ex.GetType().Name}: {ex.Message})", ex);
        }
    }

    /// <inheritdoc />
    public async Task<StorageObjectInfo?> GetInfoAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var statArgs = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(key);

            var stat = await _minioClient.StatObjectAsync(statArgs, ct);
            if (stat is null || string.IsNullOrEmpty(stat.ObjectName))
            {
                return null;
            }

            var rawEtag = stat.ETag?.Trim('"') ?? string.Empty;
            var etag = string.IsNullOrEmpty(rawEtag) ? $"\"{stat.Size}\"" : $"\"{rawEtag}\"";
            var contentType = string.IsNullOrWhiteSpace(stat.ContentType) ? "application/octet-stream" : stat.ContentType;

            return new StorageObjectInfo(key, stat.Size, etag, contentType);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MinIO nesne bilgisi okuma hatası: {Key}", key);
            throw new ExternalServiceException("MinIO", $"Nesne bilgisi alınamadı: {key}", ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var removeArgs = new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(key);

            await _minioClient.RemoveObjectAsync(removeArgs, ct);
        }
        catch (ObjectNotFoundException)
        {
            // İdempotent silme: zaten yoksa hata fırlatılmaz.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MinIO nesne silme hatası: {Key}", key);
            throw new ExternalServiceException("MinIO", $"Nesne silinemedi: {key}", ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        try
        {
            var listArgs = new ListObjectsArgs()
                .WithBucket(_bucketName)
                .WithPrefix(prefix)
                .WithRecursive(true);

            var batch = new List<string>(1000);
            var totalDeleted = 0;

            await foreach (var item in _minioClient.ListObjectsEnumAsync(listArgs, ct))
            {
                if (string.IsNullOrEmpty(item.Key))
                {
                    continue;
                }

                batch.Add(item.Key);
                if (batch.Count >= 1000)
                {
                    var removeArgs = new RemoveObjectsArgs()
                        .WithBucket(_bucketName)
                        .WithObjects(batch);
                    await _minioClient.RemoveObjectsAsync(removeArgs, ct);
                    totalDeleted += batch.Count;
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                var removeArgs = new RemoveObjectsArgs()
                    .WithBucket(_bucketName)
                    .WithObjects(batch);
                await _minioClient.RemoveObjectsAsync(removeArgs, ct);
                totalDeleted += batch.Count;
            }

            _logger.LogInformation("MinIO önek altındaki {Count} nesne silindi: {Prefix}", totalDeleted, prefix);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MinIO önek silme hatası: {Prefix}", prefix);
            throw new ExternalServiceException("MinIO", $"Önek silinemedi: {prefix}", ex);
        }
    }
}
