using System.Collections.Concurrent;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Progress;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Helpers;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Repositories;

namespace SesliKitap.Business.Services;

/// <summary>
/// Kitap dinleme konumu okuma, son-yazan-kazanır (<c>istemci_zamani</c>) senkronizasyonu ve SignalR bildirim servisi (Bölüm 4.15.4).
/// </summary>
public sealed class ProgressManager : IProgressService
{
    private static readonly ConcurrentDictionary<Guid, DateTime> LastNotificationTimes = new();
    private static readonly TimeSpan NotificationThrottle = TimeSpan.FromSeconds(5);

    private readonly IBookRepository _bookRepository;
    private readonly IChapterRepository _chapterRepository;
    private readonly IProgressRepository _progressRepository;
    private readonly ICacheService _cacheService;
    private readonly INotificationService _notificationService;
    private readonly IClock _clock;
    private readonly IValidator<ProgressUpdateRequest> _validator;

    /// <summary>
    /// <see cref="ProgressManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ProgressManager(
        IBookRepository bookRepository,
        IChapterRepository chapterRepository,
        IProgressRepository progressRepository,
        ICacheService cacheService,
        INotificationService notificationService,
        IClock clock,
        IValidator<ProgressUpdateRequest> validator)
    {
        _bookRepository = bookRepository;
        _chapterRepository = chapterRepository;
        _progressRepository = progressRepository;
        _cacheService = cacheService;
        _notificationService = notificationService;
        _clock = clock;
        _validator = validator;
    }

    /// <inheritdoc />
    public async Task<ProgressDto?> GetAsync(Guid bookId, CancellationToken ct = default)
    {
        var bookExists = await _bookRepository.AnyAsync(b => b.Id == bookId, ct);
        if (!bookExists)
        {
            throw new NotFoundException("Kitap bulunamadı.");
        }

        var progress = await _progressRepository.GetByBookAsync(bookId, ct);
        if (progress is null)
        {
            return null;
        }

        var siraNo = progress.Chapter?.SiraNo ?? 1;
        return MapToDto(progress, siraNo);
    }

    /// <inheritdoc />
    public async Task<ProgressDto> UpdateAsync(
        Guid bookId,
        ProgressUpdateRequest request,
        string? senderConnectionId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw new BusinessException(
                validation.Errors[0].ErrorMessage,
                ErrorCodes.ValidationFailed);
        }

        var now = _clock.UtcNow.UtcDateTime;
        var clientTimeUtc = request.IstemciZamani.Kind == DateTimeKind.Utc
            ? request.IstemciZamani
            : request.IstemciZamani.ToUniversalTime();

        if (clientTimeUtc > now.AddMinutes(5))
        {
            throw new BusinessException(
                "İstemci zaman damgası gelecekte en fazla 5 dakika ileride olabilir.",
                ErrorCodes.ValidationFailed);
        }

        var book = await _bookRepository.GetByIdAsync(bookId, ct)
                   ?? throw new NotFoundException("Kitap bulunamadı.");

        var chapters = await _chapterRepository.GetSummariesAsync(bookId, ct);
        var targetChapter = chapters.FirstOrDefault(c => c.Id == request.ChapterId);
        if (targetChapter is null)
        {
            throw new BusinessException(
                "Belirtilen bölüm bu kitaba ait değil.",
                ErrorCodes.ValidationFailed);
        }

        var chapterDuration = targetChapter.SureSn ?? DurationEstimator.EstimateAudioSeconds(targetChapter.KelimeSayisi);
        if (chapterDuration > 0 && request.PozisyonSn > chapterDuration + 5.0)
        {
            throw new BusinessException(
                $"Dinleme konumu ({request.PozisyonSn:F1} sn) bölüm süresini ({chapterDuration:F1} sn) aşamaz.",
                ErrorCodes.ValidationFailed);
        }

        // Son-yazan-kazanır ön kontrolü
        var existing = await _progressRepository.GetByBookAsync(bookId, ct);
        if (existing is not null && clientTimeUtc <= existing.IstemciZamani)
        {
            var existingSiraNo = existing.Chapter?.SiraNo
                                 ?? chapters.FirstOrDefault(c => c.Id == existing.ChapterId)?.SiraNo
                                 ?? 1;
            return MapToDto(existing, existingSiraNo);
        }

        // Tamamlanma yüzdesi hesabı (dahil olan bölümler üzerinden)
        var includedChapters = chapters
            .Where(c => c.DahilMi)
            .OrderBy(c => c.SiraNo)
            .ToList();

        double totalBookDuration = 0;
        double elapsedBeforeChapter = 0;

        foreach (var ch in includedChapters)
        {
            var dur = ch.SureSn ?? DurationEstimator.EstimateAudioSeconds(ch.KelimeSayisi);
            totalBookDuration += dur;
            if (ch.SiraNo < targetChapter.SiraNo)
            {
                elapsedBeforeChapter += dur;
            }
        }

        double completionPercent = 0;
        if (request.TamamlandiMi)
        {
            completionPercent = 100.0;
        }
        else if (totalBookDuration > 0)
        {
            var totalElapsed = elapsedBeforeChapter + Math.Min(request.PozisyonSn, chapterDuration > 0 ? chapterDuration : request.PozisyonSn);
            completionPercent = Math.Clamp(Math.Round((totalElapsed / totalBookDuration) * 100.0, 2), 0.0, 100.0);
        }

        var candidate = new ListeningProgress
        {
            Id = existing?.Id ?? Guid.CreateVersion7(),
            BookId = bookId,
            ChapterId = request.ChapterId,
            PozisyonSn = Math.Round(request.PozisyonSn, 2),
            OynatmaHizi = Math.Round(request.OynatmaHizi, 2),
            TamamlanmaYuzdesi = completionPercent,
            CihazKimligi = request.CihazKimligi.Trim(),
            CihazAdi = string.IsNullOrWhiteSpace(request.CihazAdi) ? null : request.CihazAdi.Trim(),
            IstemciZamani = clientTimeUtc,
            TamamlandiMi = request.TamamlandiMi
        };

        ListeningProgress winner;
        try
        {
            winner = await _progressRepository.UpsertAsync(candidate, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            winner = await _progressRepository.UpsertAsync(candidate, ct);
        }

        await _cacheService.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);
        await _cacheService.RemoveAsync(CacheKeys.Book(bookId), ct);

        var winnerSiraNo = winner.Chapter?.SiraNo
                           ?? chapters.FirstOrDefault(c => c.Id == winner.ChapterId)?.SiraNo
                           ?? targetChapter.SiraNo;

        // Kazanan kayıt bu istekse ve kitap başına 5 sn throttle süresi geçtiyse diğer cihazlara bildir
        if (winner.IstemciZamani == clientTimeUtc && ShouldNotify(bookId, now))
        {
            await _notificationService.ProgressUpdatedAsync(
                bookId,
                winner.ChapterId,
                winnerSiraNo,
                winner.PozisyonSn,
                winner.OynatmaHizi,
                winner.CihazKimligi,
                winner.CihazAdi,
                winner.IstemciZamani,
                senderConnectionId,
                ct);
        }

        return MapToDto(winner, winnerSiraNo);
    }

    private static bool ShouldNotify(Guid bookId, DateTime nowUtc)
    {
        if (LastNotificationTimes.TryGetValue(bookId, out var lastTime) &&
            nowUtc - lastTime < NotificationThrottle)
        {
            return false;
        }

        LastNotificationTimes[bookId] = nowUtc;
        return true;
    }

    private static ProgressDto MapToDto(ListeningProgress entity, int bolumSiraNo)
    {
        return new ProgressDto(
            entity.BookId,
            entity.ChapterId,
            bolumSiraNo,
            entity.PozisyonSn,
            entity.OynatmaHizi,
            entity.TamamlanmaYuzdesi,
            entity.CihazKimligi,
            entity.CihazAdi,
            entity.IstemciZamani,
            entity.TamamlandiMi,
            entity.GuncellemeTarihi ?? entity.OlusturmaTarihi);
    }
}
