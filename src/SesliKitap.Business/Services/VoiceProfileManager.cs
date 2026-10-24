using FluentValidation;
using Microsoft.Extensions.Options;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Voice;
using SesliKitap.Business.Events;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Helpers;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;

namespace SesliKitap.Business.Services;

/// <summary>
/// <see cref="IVoiceProfileService"/> sözleşmesinin iş mantığı uygulaması (Bölüm 4.15.4).
/// </summary>
public sealed class VoiceProfileManager : IVoiceProfileService
{
    private readonly IVoiceProfileRepository _voiceRepository;
    private readonly IFileStorageService _storage;
    private readonly ICacheService _cache;
    private readonly IEventPublisher _publisher;
    private readonly INotificationService _notifications;
    private readonly ILogService _log;
    private readonly IValidator<VoiceProfileCreateRequest> _createValidator;
    private readonly IValidator<VoiceProfileUpdateRequest> _updateValidator;
    private readonly SynthesisDefaultsOptions _synthesisDefaults;
    private readonly CacheOptions _cacheOptions;

    /// <summary>
    /// <see cref="VoiceProfileManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public VoiceProfileManager(
        IVoiceProfileRepository voiceRepository,
        IFileStorageService storage,
        ICacheService cache,
        IEventPublisher publisher,
        INotificationService notifications,
        ILogService log,
        IValidator<VoiceProfileCreateRequest> createValidator,
        IValidator<VoiceProfileUpdateRequest> updateValidator,
        IOptions<SynthesisDefaultsOptions> synthesisDefaults,
        IOptions<CacheOptions> cacheOptions)
    {
        _voiceRepository = voiceRepository;
        _storage = storage;
        _cache = cache;
        _publisher = publisher;
        _notifications = notifications;
        _log = log;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _synthesisDefaults = synthesisDefaults.Value;
        _cacheOptions = cacheOptions.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VoiceProfileDto>> GetListAsync(CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync<IReadOnlyList<VoiceProfileDto>>(
            CacheKeys.VoiceList,
            async token =>
            {
                var entities = await _voiceRepository.GetAllOrderedAsync(token);
                return entities.Select(MapToDto).ToList();
            },
            TimeSpan.FromMinutes(_cacheOptions.VoiceListMinutes),
            ct);
    }

    /// <inheritdoc />
    public async Task<VoiceProfileDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _voiceRepository.GetByIdAsync(id, ct)
                     ?? throw new NotFoundException("Ses profili", id);

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<VoiceProfileDto> CreateAsync(
        VoiceProfileCreateRequest request,
        string traceId,
        CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);

        var trimmedName = request.Ad.Trim();
        if (await _voiceRepository.ExistsByNameAsync(trimmedName, excludeId: null, ct))
        {
            throw new ConflictException($"'{trimmedName}' adında bir ses profili zaten mevcut.");
        }

        await using (var signatureStream = request.Ses.OpenReadStream())
        {
            var validSignature = await FileSignatureValidator.IsSupportedAudioAsync(signatureStream, ct);
            if (!validSignature)
            {
                throw new BusinessException("Yüklenen dosyanın ses imzası (magic byte) desteklenen bir ses formatıyla uyuşmuyor.");
            }
        }

        var profileId = Guid.CreateVersion7();
        var ext = Path.GetExtension(request.Ses.FileName)?.ToLowerInvariant() ?? ".wav";
        var rawObjectKey = StorageKeys.VoiceRaw(profileId, ext);
        var contentType = ContentTypeMap.GetContentType(ext);

        await using (var uploadStream = request.Ses.OpenReadStream())
        {
            await _storage.UploadAsync(rawObjectKey, uploadStream, request.Ses.Length, contentType, ct);
        }

        var hasAnyExisting = (await _voiceRepository.GetAllOrderedAsync(ct)).Count > 0;
        var entity = new VoiceProfile
        {
            Id = profileId,
            Ad = trimmedName,
            Aciklama = string.IsNullOrWhiteSpace(request.Aciklama) ? null : request.Aciklama.Trim(),
            Cinsiyet = request.Cinsiyet,
            HamSesYolu = rawObjectKey,
            ReferansMetin = string.IsNullOrWhiteSpace(request.ReferansMetin) ? null : request.ReferansMetin.Trim(),
            Parametreler = new VoiceParameters(),
            VarsayilanMi = !hasAnyExisting,
            KullanimOnayi = request.KullanimOnayi,
            Durum = SesProfiliDurumu.Hazirlaniyor
        };

        await _voiceRepository.AddAsync(entity, ct);
        await _voiceRepository.SaveChangesAsync(ct);

        try
        {
            await _publisher.PublishAsync(
                new VoiceProfileUploadedEvent
                {
                    TraceId = traceId,
                    VoiceProfileId = profileId,
                    HamSesYolu = rawObjectKey,
                    ReferansMetin = entity.ReferansMetin,
                    OrnekMetin = _synthesisDefaults.SampleParagraph
                },
                ct);
        }
        catch (Exception ex)
        {
            entity.Durum = SesProfiliDurumu.Hata;
            entity.HataMesaji = "Ses profili hazırlama kuyruğuna iletilemedi.";
            _voiceRepository.Update(entity);
            await _voiceRepository.SaveChangesAsync(ct);
            await _log.LogFunctionErrorAsync(ErrorCodes.VoiceUploadError, ex, new { profileId, trimmedName }, traceId, ct: ct);
            throw new ExternalServiceException("RabbitMQ", "Ses profili hazırlama olayı yayınlanamadı.", ex);
        }

        await _cache.RemoveAsync(CacheKeys.VoiceList, ct);
        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<VoiceProfileDto> UpdateAsync(
        Guid id,
        VoiceProfileUpdateRequest request,
        CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);

        var entity = await _voiceRepository.GetByIdAsync(id, ct)
                     ?? throw new NotFoundException("Ses profili", id);

        var trimmedName = request.Ad.Trim();
        if (await _voiceRepository.ExistsByNameAsync(trimmedName, excludeId: id, ct))
        {
            throw new ConflictException($"'{trimmedName}' adında başka bir ses profili zaten mevcut.");
        }

        entity.Ad = trimmedName;
        entity.Aciklama = string.IsNullOrWhiteSpace(request.Aciklama) ? null : request.Aciklama.Trim();
        entity.Cinsiyet = request.Cinsiyet;
        entity.ReferansMetin = string.IsNullOrWhiteSpace(request.ReferansMetin) ? null : request.ReferansMetin.Trim();

        if (request.Parametreler is not null)
        {
            entity.Parametreler = new VoiceParameters
            {
                CfgValue = request.Parametreler.CfgValue,
                InferenceTimesteps = request.Parametreler.InferenceTimesteps,
                KonusmaHizi = request.Parametreler.KonusmaHizi,
                Tohum = request.Parametreler.Tohum
            };
        }

        _voiceRepository.Update(entity);
        await _voiceRepository.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.VoiceList, ct);

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task SetDefaultAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _voiceRepository.GetByIdAsync(id, ct)
                     ?? throw new NotFoundException("Ses profili", id);

        if (entity.Durum != SesProfiliDurumu.Hazir)
        {
            throw new BusinessException("Yalnızca 'Hazir' durumundaki bir ses profili varsayılan yapılabilir.");
        }

        await _voiceRepository.SetDefaultAsync(id, ct);
        await _cache.RemoveAsync(CacheKeys.VoiceList, ct);
    }

    /// <inheritdoc />
    public async Task<VoiceProfileDto> RegenerateSampleAsync(
        Guid id,
        VoiceRegenerateSampleRequest request,
        string traceId,
        CancellationToken ct = default)
    {
        var entity = await _voiceRepository.GetByIdAsync(id, ct)
                     ?? throw new NotFoundException("Ses profili", id);

        var sampleText = string.IsNullOrWhiteSpace(request.Metin)
            ? _synthesisDefaults.SampleParagraph
            : request.Metin.Trim();

        if (sampleText.Length > 500)
        {
            throw new BusinessException("Örnek metin en fazla 500 karakter olabilir.");
        }

        entity.Durum = SesProfiliDurumu.Hazirlaniyor;
        entity.HataMesaji = null;
        _voiceRepository.Update(entity);
        await _voiceRepository.SaveChangesAsync(ct);

        await _publisher.PublishAsync(
            new VoiceProfileUploadedEvent
            {
                TraceId = traceId,
                VoiceProfileId = entity.Id,
                HamSesYolu = entity.HamSesYolu,
                ReferansMetin = entity.ReferansMetin,
                OrnekMetin = sampleText
            },
            ct);

        await _cache.RemoveAsync(CacheKeys.VoiceList, ct);
        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<(Stream Stream, string ContentType)> OpenSampleAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _voiceRepository.GetByIdAsync(id, ct)
                     ?? throw new NotFoundException("Ses profili", id);

        if (string.IsNullOrWhiteSpace(entity.OrnekSesYolu))
        {
            throw new NotFoundException("Ses profili örnek sesi", id);
        }

        var info = await _storage.GetInfoAsync(entity.OrnekSesYolu, ct)
                   ?? throw new NotFoundException("Ses profili örnek ses dosyası", id);

        var ms = new MemoryStream((int)Math.Min(info.Size, 10 * 1024 * 1024));
        await _storage.CopyToAsync(entity.OrnekSesYolu, ms, offset: 0, length: null, ct);
        ms.Position = 0;

        return (ms, "audio/ogg");
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _voiceRepository.GetByIdAsync(id, ct)
                     ?? throw new NotFoundException("Ses profili", id);

        entity.VarsayilanMi = false;
        _voiceRepository.Delete(entity);
        await _voiceRepository.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.VoiceList, ct);
    }

    /// <inheritdoc />
    public async Task HandleReadyAsync(VoiceProfileReadyEvent readyEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(readyEvent);
        var entity = await _voiceRepository.GetByIdAsync(readyEvent.VoiceProfileId, ct);
        if (entity is null)
        {
            return;
        }

        // İdempotency kontrolü: aynı referans hash ve örnek ses yolu zaten işlenmişse tekrar ekleme yapma
        if (readyEvent.Basarili
            && entity.Durum == SesProfiliDurumu.Hazir
            && string.Equals(entity.ReferansHash, readyEvent.ReferansHash, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entity.OrnekSesYolu, readyEvent.OrnekSesYolu, StringComparison.Ordinal))
        {
            return;
        }

        if (readyEvent.Basarili)
        {
            entity.ReferansSesYolu = readyEvent.ReferansSesYolu;
            entity.ReferansHash = readyEvent.ReferansHash;
            entity.ReferansSureSn = readyEvent.ReferansSureSn;
            entity.OrnekSesYolu = readyEvent.OrnekSesYolu;
            entity.Durum = SesProfiliDurumu.Hazir;
            entity.HataMesaji = null;
            entity.GpuMs += Math.Max(0, readyEvent.GpuMs);
        }
        else
        {
            entity.Durum = SesProfiliDurumu.Hata;
            entity.HataMesaji = string.IsNullOrWhiteSpace(readyEvent.HataMesaji)
                ? "Ses profili hazırlanırken beklenmeyen bir hata oluştu."
                : readyEvent.HataMesaji;
            entity.GpuMs += Math.Max(0, readyEvent.GpuMs);
        }

        _voiceRepository.Update(entity);
        await _voiceRepository.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.VoiceList, ct);

        await _notifications.VoiceProfileReadyAsync(
            entity.Id,
            readyEvent.Basarili,
            entity.HataMesaji,
            ct);
    }

    private static VoiceProfileDto MapToDto(VoiceProfile entity)
    {
        var p = entity.Parametreler ?? new VoiceParameters();
        return new VoiceProfileDto(
            Id: entity.Id,
            Ad: entity.Ad,
            Aciklama: entity.Aciklama,
            Cinsiyet: entity.Cinsiyet,
            ReferansMetin: entity.ReferansMetin,
            ReferansSureSn: entity.ReferansSureSn,
            OrnekSesVar: !string.IsNullOrWhiteSpace(entity.OrnekSesYolu),
            Parametreler: new VoiceParametersDto(
                CfgValue: p.CfgValue,
                InferenceTimesteps: p.InferenceTimesteps,
                KonusmaHizi: p.KonusmaHizi,
                Tohum: p.Tohum),
            VarsayilanMi: entity.VarsayilanMi,
            Durum: entity.Durum,
            HataMesaji: entity.HataMesaji,
            OlusturmaTarihi: entity.OlusturmaTarihi);
    }
}
