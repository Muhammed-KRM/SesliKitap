using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Pronunciation;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Helpers;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Context;
using SesliKitap.Data.Entities;

namespace SesliKitap.Business.Services;

/// <summary>
/// Telaffuz sözlüğü CRUD işlemlerini, Redis önbelleklemesini ve metin önizlemesini yöneten servis (Faz 8).
/// </summary>
public sealed class PronunciationManager : IPronunciationService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private readonly AppDbContext _db;
    private readonly ICacheService _cache;
    private readonly IValidator<PronunciationCreateRequest> _createValidator;
    private readonly IValidator<PronunciationUpdateRequest> _updateValidator;
    private readonly IValidator<PronunciationPreviewRequest> _previewValidator;

    /// <summary>
    /// <see cref="PronunciationManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public PronunciationManager(
        AppDbContext db,
        ICacheService cache,
        IValidator<PronunciationCreateRequest> createValidator,
        IValidator<PronunciationUpdateRequest> updateValidator,
        IValidator<PronunciationPreviewRequest> previewValidator)
    {
        _db = db;
        _cache = cache;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _previewValidator = previewValidator;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PronunciationDto>> GetAllAsync(CancellationToken ct = default)
    {
        var cached = await _cache.GetAsync<List<PronunciationDto>>(CacheKeys.PronunciationList, ct);
        if (cached is not null)
        {
            return cached;
        }

        var entries = await _db.PronunciationEntries
            .AsNoTracking()
            .OrderBy(x => x.Kelime)
            .Select(x => new PronunciationDto(
                x.Id,
                x.Kelime,
                x.Okunus,
                x.BuyukKucukDuyarli,
                x.AktifMi,
                x.Aciklama,
                x.OlusturmaTarihi))
            .ToListAsync(ct);

        await _cache.SetAsync(CacheKeys.PronunciationList, entries, CacheTtl, ct);
        return entries;
    }

    /// <inheritdoc />
    public async Task<PronunciationDto> CreateAsync(PronunciationCreateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            throw new BusinessException(message, ErrorCodes.ValidationFailed);
        }

        var trimmedWord = request.Kelime.Trim();
        var normalizedNew = TurkishTextHelper.ToLowerTr(trimmedWord);

        var existingWords = await _db.PronunciationEntries
            .AsNoTracking()
            .Select(x => new { x.Id, x.Kelime })
            .ToListAsync(ct);

        var duplicate = existingWords.FirstOrDefault(
            x => TurkishTextHelper.ToLowerTr(x.Kelime) == normalizedNew);
        if (duplicate is not null)
        {
            throw new ConflictException(
                $"'{trimmedWord}' kelimesi telaffuz sözlüğünde zaten mevcut.",
                duplicate.Id);
        }

        var entity = new PronunciationEntry
        {
            Id = Guid.CreateVersion7(),
            Kelime = trimmedWord,
            Okunus = request.Okunus.Trim(),
            BuyukKucukDuyarli = request.BuyukKucukDuyarli,
            AktifMi = request.AktifMi,
            Aciklama = string.IsNullOrWhiteSpace(request.Aciklama) ? null : request.Aciklama.Trim()
        };

        await _db.PronunciationEntries.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.PronunciationList, ct);

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<PronunciationDto> UpdateAsync(
        Guid id,
        PronunciationUpdateRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            throw new BusinessException(message, ErrorCodes.ValidationFailed);
        }

        var entity = await _db.PronunciationEntries.FirstOrDefaultAsync(x => x.Id == id, ct)
                     ?? throw new NotFoundException("Telaffuz sözlüğü girdisi", id);

        var trimmedWord = request.Kelime.Trim();
        var normalizedNew = TurkishTextHelper.ToLowerTr(trimmedWord);

        var otherWords = await _db.PronunciationEntries
            .AsNoTracking()
            .Where(x => x.Id != id)
            .Select(x => new { x.Id, x.Kelime })
            .ToListAsync(ct);

        var duplicate = otherWords.FirstOrDefault(
            x => TurkishTextHelper.ToLowerTr(x.Kelime) == normalizedNew);
        if (duplicate is not null)
        {
            throw new ConflictException(
                $"'{trimmedWord}' kelimesi telaffuz sözlüğünde başka bir kayıtta zaten mevcut.",
                duplicate.Id);
        }

        entity.Kelime = trimmedWord;
        entity.Okunus = request.Okunus.Trim();
        entity.BuyukKucukDuyarli = request.BuyukKucukDuyarli;
        entity.AktifMi = request.AktifMi;
        entity.Aciklama = string.IsNullOrWhiteSpace(request.Aciklama) ? null : request.Aciklama.Trim();

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.PronunciationList, ct);

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.PronunciationEntries.FirstOrDefaultAsync(x => x.Id == id, ct)
                     ?? throw new NotFoundException("Telaffuz sözlüğü girdisi", id);

        _db.PronunciationEntries.Remove(entity);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.PronunciationList, ct);
    }

    /// <inheritdoc />
    public async Task<PronunciationPreviewResponse> PreviewAsync(
        PronunciationPreviewRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _previewValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            throw new BusinessException(message, ErrorCodes.ValidationFailed);
        }

        var entries = await GetAllAsync(ct);
        var activeEntries = entries
            .Where(x => x.AktifMi)
            .OrderByDescending(x => x.Kelime.Length)
            .ToList();

        var result = request.Metin;
        var replacedCount = 0;

        foreach (var entry in activeEntries)
        {
            var escaped = Regex.Escape(entry.Kelime);
            var pattern = $@"(?<![\p{{L}}\p{{N}}_]){escaped}(?:['’][\p{{L}}]+)?(?![\p{{L}}\p{{N}}_])";
            var options = entry.BuyukKucukDuyarli
                ? RegexOptions.CultureInvariant
                : RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

            result = Regex.Replace(
                result,
                pattern,
                match =>
                {
                    replacedCount++;
                    var matchedText = match.Value;
                    var apostropheIdx = matchedText.IndexOfAny(['\'', '’']);
                    if (apostropheIdx >= 0 && apostropheIdx >= entry.Kelime.Length)
                    {
                        var suffix = matchedText[(apostropheIdx + 1)..];
                        return entry.Okunus + suffix;
                    }

                    return entry.Okunus;
                },
                options);
        }

        return new PronunciationPreviewResponse(request.Metin, result, replacedCount);
    }

    private static PronunciationDto MapToDto(PronunciationEntry x) =>
        new(
            x.Id,
            x.Kelime,
            x.Okunus,
            x.BuyukKucukDuyarli,
            x.AktifMi,
            x.Aciklama,
            x.OlusturmaTarihi);
}
