using FluentValidation;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Chapter;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Helpers;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;

namespace SesliKitap.Business.Services;

/// <summary>
/// <see cref="IChapterService"/> sözleşmesinin bölüm okuma, metin düzenleme, birleştirme, bölme ve sıralama uygulaması.
/// </summary>
public sealed class ChapterManager : IChapterService
{
    private readonly IBookRepository _bookRepository;
    private readonly IChapterRepository _chapterRepository;
    private readonly ICacheService _cache;
    private readonly ILogService _logService;
    private readonly IValidator<ChapterUpdateRequest> _updateValidator;
    private readonly IValidator<ChapterMergeRequest> _mergeValidator;
    private readonly IValidator<ChapterSplitRequest> _splitValidator;
    private readonly IValidator<ChapterOrderRequest> _orderValidator;
    private readonly IValidator<ChapterInclusionRequest> _inclusionValidator;

    /// <summary>
    /// <see cref="ChapterManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ChapterManager(
        IBookRepository bookRepository,
        IChapterRepository chapterRepository,
        ICacheService cache,
        ILogService logService,
        IValidator<ChapterUpdateRequest> updateValidator,
        IValidator<ChapterMergeRequest> mergeValidator,
        IValidator<ChapterSplitRequest> splitValidator,
        IValidator<ChapterOrderRequest> orderValidator,
        IValidator<ChapterInclusionRequest> inclusionValidator)
    {
        _bookRepository = bookRepository;
        _chapterRepository = chapterRepository;
        _cache = cache;
        _logService = logService;
        _updateValidator = updateValidator;
        _mergeValidator = mergeValidator;
        _splitValidator = splitValidator;
        _orderValidator = orderValidator;
        _inclusionValidator = inclusionValidator;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChapterSummaryDto>> GetSummariesByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        var bookExists = await _bookRepository.AnyAsync(b => b.Id == bookId, ct);
        if (!bookExists)
        {
            throw new NotFoundException("Kitap", bookId);
        }

        var rows = await _chapterRepository.GetSummariesAsync(bookId, ct);
        return rows.Select(MapToSummaryDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ChapterDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var chapter = await _chapterRepository.GetWithTextAsync(id, asNoTracking: true, ct)
            ?? throw new NotFoundException("Bölüm", id);

        return MapToDetailDto(chapter);
    }

    /// <inheritdoc />
    public async Task<ChapterDetailDto> UpdateAsync(Guid id, ChapterUpdateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw new BusinessException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var chapter = await _chapterRepository.GetWithTextAsync(id, asNoTracking: false, ct)
            ?? throw new NotFoundException("Bölüm", id);

        if (request.MetinSurumu != chapter.MetinSurumu)
        {
            await _logService.LogFunctionWarningAsync(
                ErrorCodes.ChapterUpdateConflict,
                "Bölüm metin sürümü uyuşmazlığı (iyimser kilitleme).",
                new { ChapterId = id, ClientVersion = request.MetinSurumu, ServerVersion = chapter.MetinSurumu },
                ct: ct);

            throw new ConflictException(
                "Bu bölüm başka bir yerde değiştirildi. Lütfen sayfayı yenileyip tekrar deneyin.",
                chapter.Id);
        }

        var normalizedText = request.TemizMetin.Trim();
        var textChanged = !string.Equals(chapter.TemizMetin, normalizedText, StringComparison.Ordinal);

        chapter.Baslik = request.Baslik.Trim();
        chapter.DahilMi = request.DahilMi;
        if (request.DahilMi)
        {
            chapter.OtomatikHaricNedeni = null;
        }

        if (textChanged)
        {
            chapter.TemizMetin = normalizedText;
            chapter.MetinSurumu++;
            chapter.KelimeSayisi = TurkishTextHelper.CountWords(normalizedText);
            chapter.KarakterSayisi = normalizedText.Length;

            if (chapter.SesDurumu == BolumSesDurumu.Hazir || chapter.OpusYolu is not null)
            {
                chapter.SesDurumu = BolumSesDurumu.GuncelDegil;
            }
        }

        _chapterRepository.Update(chapter);
        await _chapterRepository.SaveChangesAsync(ct);

        await _chapterRepository.RecalculateBookWordCountAsync(chapter.BookId, ct);
        await InvalidateBookCacheAsync(chapter.BookId, ct);

        return MapToDetailDto(chapter);
    }

    /// <inheritdoc />
    public async Task<ChapterDetailDto> MergeAsync(Guid bookId, ChapterMergeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _mergeValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw new BusinessException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        await EnsureNoActiveSynthesisAsync(bookId, ct);

        var allChapters = await _chapterRepository.GetByBookIdOrderedAsync(bookId, asNoTracking: false, ct);
        if (allChapters.Count == 0)
        {
            throw new NotFoundException("Kitap", bookId);
        }

        var idSet = request.ChapterIds.ToHashSet();
        var selected = allChapters.Where(c => idSet.Contains(c.Id)).OrderBy(c => c.SiraNo).ToList();

        if (selected.Count != request.ChapterIds.Count)
        {
            throw new BusinessException("Birleştirilecek bölümlerin tamamı bu kitaba ait olmalıdır.");
        }

        for (var i = 1; i < selected.Count; i++)
        {
            if (selected[i].SiraNo != selected[i - 1].SiraNo + 1)
            {
                throw new BusinessException("Yalnızca ardışık sıra numaralı bölümler birleştirilebilir.");
            }
        }

        var primary = selected[0];
        var toRemove = selected.Skip(1).ToList();
        var lastMergedSiraNo = selected[^1].SiraNo;
        var mergedText = string.Join("\n\n", selected.Select(c => c.TemizMetin.Trim()).Where(t => t.Length > 0));

        var (ruleIncluded, ruleReason) = ChapterTitleRules.Evaluate(primary.Baslik);

        primary.TemizMetin = mergedText;
        primary.MetinSurumu++;
        primary.KelimeSayisi = TurkishTextHelper.CountWords(mergedText);
        primary.KarakterSayisi = mergedText.Length;
        primary.DahilMi = primary.DahilMi && ruleIncluded;
        primary.OtomatikHaricNedeni = !primary.DahilMi ? (primary.OtomatikHaricNedeni ?? ruleReason) : null;
        primary.SesDurumu = primary.OpusYolu is not null ? BolumSesDurumu.GuncelDegil : BolumSesDurumu.Yok;

        await _bookRepository.ExecuteInTransactionAsync(async token =>
        {
            foreach (var rem in toRemove)
            {
                _chapterRepository.Delete(rem);
            }

            _chapterRepository.Update(primary);
            await _chapterRepository.SaveChangesAsync(token);

            await _chapterRepository.ShiftOrderAsync(bookId, lastMergedSiraNo + 1, -toRemove.Count, token);
            await _chapterRepository.RecalculateBookWordCountAsync(bookId, token);
        }, ct);

        await InvalidateBookCacheAsync(bookId, ct);
        return MapToDetailDto(primary);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChapterSummaryDto>> SplitAsync(Guid id, ChapterSplitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _splitValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw new BusinessException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var chapter = await _chapterRepository.GetWithTextAsync(id, asNoTracking: false, ct)
            ?? throw new NotFoundException("Bölüm", id);

        await EnsureNoActiveSynthesisAsync(chapter.BookId, ct);

        var text = chapter.TemizMetin;
        if (request.KarakterIndeksi <= 0 || request.KarakterIndeksi >= text.Length)
        {
            throw new BusinessException("Bölme indeksi bölüm metni sınırları içinde olmalıdır.");
        }

        var splitPos = FindNearestParagraphBoundary(text, request.KarakterIndeksi);
        var firstPart = text[..splitPos].Trim();
        var secondPart = text[splitPos..].Trim();

        if (string.IsNullOrWhiteSpace(firstPart) || string.IsNullOrWhiteSpace(secondPart))
        {
            throw new BusinessException("Bölme sonucunda her iki bölümde de anlamlı metin kalmalıdır.");
        }

        var newTitle = !string.IsNullOrWhiteSpace(request.YeniBaslik)
            ? request.YeniBaslik.Trim()
            : $"{chapter.Baslik} (2. Kısım)";

        var (ruleIncluded, ruleReason) = ChapterTitleRules.Evaluate(newTitle);

        chapter.TemizMetin = firstPart;
        chapter.MetinSurumu++;
        chapter.KelimeSayisi = TurkishTextHelper.CountWords(firstPart);
        chapter.KarakterSayisi = firstPart.Length;
        chapter.SesDurumu = chapter.OpusYolu is not null ? BolumSesDurumu.GuncelDegil : BolumSesDurumu.Yok;

        var secondChapter = new BookChapter
        {
            Id = Guid.CreateVersion7(),
            BookId = chapter.BookId,
            SiraNo = chapter.SiraNo + 1,
            Baslik = newTitle,
            TemizMetin = secondPart,
            KelimeSayisi = TurkishTextHelper.CountWords(secondPart),
            KarakterSayisi = secondPart.Length,
            DahilMi = chapter.DahilMi && ruleIncluded,
            OtomatikHaricNedeni = !ruleIncluded ? ruleReason : null,
            MetinSurumu = 1,
            SesDurumu = BolumSesDurumu.Yok
        };

        await _bookRepository.ExecuteInTransactionAsync(async token =>
        {
            await _chapterRepository.ShiftOrderAsync(chapter.BookId, chapter.SiraNo + 1, +1, token);
            _chapterRepository.Update(chapter);
            await _chapterRepository.AddAsync(secondChapter, token);
            await _chapterRepository.SaveChangesAsync(token);
            await _chapterRepository.RecalculateBookWordCountAsync(chapter.BookId, token);
        }, ct);

        await InvalidateBookCacheAsync(chapter.BookId, ct);
        return await GetSummariesByBookAsync(chapter.BookId, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChapterSummaryDto>> ReorderAsync(Guid bookId, ChapterOrderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _orderValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw new BusinessException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        await EnsureNoActiveSynthesisAsync(bookId, ct);

        var chapters = await _chapterRepository.GetByBookIdOrderedAsync(bookId, asNoTracking: false, ct);
        if (chapters.Count == 0)
        {
            throw new NotFoundException("Kitap", bookId);
        }

        if (chapters.Count != request.ChapterIds.Count ||
            !chapters.Select(c => c.Id).ToHashSet().SetEquals(request.ChapterIds))
        {
            throw new BusinessException("Sıralama listesi kitaptaki tüm bölümleri eksiksiz içermelidir.");
        }

        var map = chapters.ToDictionary(c => c.Id);

        await _bookRepository.ExecuteInTransactionAsync(async token =>
        {
            // Aşama 1: Benzersiz (book_id, sira_no) kısıtlaması çakışmasını önlemek için negatif geçici sıra numaraları ata
            for (var i = 0; i < request.ChapterIds.Count; i++)
            {
                var ch = map[request.ChapterIds[i]];
                ch.SiraNo = -(i + 1);
            }

            await _chapterRepository.SaveChangesAsync(token);

            // Aşama 2: Pozitif yeni sıra numaralarını ata
            for (var i = 0; i < request.ChapterIds.Count; i++)
            {
                var ch = map[request.ChapterIds[i]];
                ch.SiraNo = i + 1;
            }

            await _chapterRepository.SaveChangesAsync(token);
        }, ct);

        await InvalidateBookCacheAsync(bookId, ct);
        return await GetSummariesByBookAsync(bookId, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChapterSummaryDto>> SetInclusionAsync(
        Guid bookId,
        ChapterInclusionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _inclusionValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw new BusinessException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var bookExists = await _bookRepository.AnyAsync(b => b.Id == bookId, ct);
        if (!bookExists)
        {
            throw new NotFoundException("Kitap", bookId);
        }

        await _chapterRepository.UpdateInclusionBulkAsync(bookId, request.ChapterIds, request.DahilMi, ct);
        await _chapterRepository.RecalculateBookWordCountAsync(bookId, ct);
        await InvalidateBookCacheAsync(bookId, ct);

        return await GetSummariesByBookAsync(bookId, ct);
    }

    private async Task EnsureNoActiveSynthesisAsync(Guid bookId, CancellationToken ct)
    {
        var book = await _bookRepository.GetByIdAsync(bookId, ct)
            ?? throw new NotFoundException("Kitap", bookId);

        var activeJob = await _cache.GetStringAsync(CacheKeys.ActiveJob(bookId), ct);
        if (!string.IsNullOrEmpty(activeJob) || book.Durum == KitapDurumu.Seslendiriliyor)
        {
            throw new ConflictException("Seslendirme devam ederken bölüm yapısı değiştirilemez.");
        }
    }

    private async Task InvalidateBookCacheAsync(Guid bookId, CancellationToken ct)
    {
        await _cache.RemoveAsync(CacheKeys.Book(bookId), ct);
        await _cache.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);
    }

    private static int FindNearestParagraphBoundary(string text, int index)
    {
        // En yakın "\n\n" paragraf sınırını ara
        var prevDoubleNewline = text.LastIndexOf("\n\n", Math.Min(index, text.Length - 1), StringComparison.Ordinal);
        var nextDoubleNewline = text.IndexOf("\n\n", index, StringComparison.Ordinal);

        if (prevDoubleNewline > 0 && nextDoubleNewline > 0)
        {
            return (index - prevDoubleNewline) <= (nextDoubleNewline - index)
                ? prevDoubleNewline + 2
                : nextDoubleNewline + 2;
        }

        if (prevDoubleNewline > 0)
        {
            return prevDoubleNewline + 2;
        }

        if (nextDoubleNewline > 0 && nextDoubleNewline + 2 < text.Length)
        {
            return nextDoubleNewline + 2;
        }

        // "\n\n" yoksa en yakın tek satır sonu ("\n") veya boşluk sınırına yuvarla
        var prevNewline = text.LastIndexOf('\n', Math.Min(index, text.Length - 1));
        if (prevNewline > 0)
        {
            return prevNewline + 1;
        }

        var nextNewline = text.IndexOf('\n', index);
        if (nextNewline > 0 && nextNewline + 1 < text.Length)
        {
            return nextNewline + 1;
        }

        var nextSpace = text.IndexOf(' ', index);
        if (nextSpace > 0 && nextSpace + 1 < text.Length)
        {
            return nextSpace + 1;
        }

        var prevSpace = text.LastIndexOf(' ', Math.Min(index, text.Length - 1));
        return prevSpace > 0 ? prevSpace + 1 : index;
    }

    private static ChapterSummaryDto MapToSummaryDto(ChapterSummaryRow c) =>
        new(
            c.Id,
            c.SiraNo,
            c.Baslik,
            c.KelimeSayisi,
            c.KarakterSayisi,
            c.DahilMi,
            c.OtomatikHaricNedeni,
            c.SesDurumu,
            c.SureSn,
            DurationEstimator.EstimateAudioSeconds(c.KelimeSayisi),
            c.MetinSurumu);

    private static ChapterDetailDto MapToDetailDto(BookChapter c) =>
        new(
            c.Id,
            c.BookId,
            c.SiraNo,
            c.Baslik,
            c.TemizMetin,
            c.KelimeSayisi,
            c.KarakterSayisi,
            c.DahilMi,
            c.OtomatikHaricNedeni,
            c.SesDurumu,
            c.SureSn,
            DurationEstimator.EstimateAudioSeconds(c.KelimeSayisi),
            c.MetinSurumu);
}
