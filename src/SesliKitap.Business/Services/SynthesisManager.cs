using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Options;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Common;
using SesliKitap.Business.DTOs.Logs;
using SesliKitap.Business.DTOs.Synthesis;
using SesliKitap.Business.Events;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;

namespace SesliKitap.Business.Services;

/// <summary>
/// <see cref="ISynthesisService"/> sözleşmesinin kitap seslendirme orkestrasyonu, maliyet tahmini, dağıtık kilit ve olay işleme uygulaması (Bölüm 4.15.4).
/// </summary>
public sealed class SynthesisManager : ISynthesisService
{
    private static readonly TimeSpan ActiveJobLockTtl = TimeSpan.FromHours(24);

    private readonly ISynthesisJobRepository _jobRepository;
    private readonly IBookRepository _bookRepository;
    private readonly IChapterRepository _chapterRepository;
    private readonly IVoiceProfileRepository _voiceRepository;
    private readonly IFileStorageService _storage;
    private readonly ICacheService _cache;
    private readonly IEventPublisher _publisher;
    private readonly INotificationService _notifications;
    private readonly ICostEstimator _costEstimator;
    private readonly ILogService _logService;
    private readonly IClock _clock;
    private readonly IValidator<SynthesisStartRequest> _startValidator;
    private readonly TtsOptions _ttsOptions;
    private readonly SynthesisDefaultsOptions _defaultsOptions;
    private readonly IPronunciationService? _pronunciationService;

    /// <summary>
    /// <see cref="SynthesisManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public SynthesisManager(
        ISynthesisJobRepository jobRepository,
        IBookRepository bookRepository,
        IChapterRepository chapterRepository,
        IVoiceProfileRepository voiceRepository,
        IFileStorageService storage,
        ICacheService cache,
        IEventPublisher publisher,
        INotificationService notifications,
        ICostEstimator costEstimator,
        ILogService logService,
        IClock clock,
        IValidator<SynthesisStartRequest> startValidator,
        IOptions<TtsOptions> ttsOptions,
        IOptions<SynthesisDefaultsOptions> defaultsOptions,
        IPronunciationService? pronunciationService = null)
    {
        ArgumentNullException.ThrowIfNull(jobRepository);
        ArgumentNullException.ThrowIfNull(bookRepository);
        ArgumentNullException.ThrowIfNull(chapterRepository);
        ArgumentNullException.ThrowIfNull(voiceRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(costEstimator);
        ArgumentNullException.ThrowIfNull(logService);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(startValidator);
        ArgumentNullException.ThrowIfNull(ttsOptions);
        ArgumentNullException.ThrowIfNull(defaultsOptions);

        _jobRepository = jobRepository;
        _bookRepository = bookRepository;
        _chapterRepository = chapterRepository;
        _voiceRepository = voiceRepository;
        _storage = storage;
        _cache = cache;
        _publisher = publisher;
        _notifications = notifications;
        _costEstimator = costEstimator;
        _logService = logService;
        _clock = clock;
        _startValidator = startValidator;
        _ttsOptions = ttsOptions.Value;
        _defaultsOptions = defaultsOptions.Value;
        _pronunciationService = pronunciationService;
    }

    /// <inheritdoc />
    public async Task<SynthesisEstimateDto> EstimateAsync(
        Guid bookId,
        Guid voiceProfileId,
        IReadOnlyList<Guid>? chapterIds,
        bool sadeceEksikler = true,
        CancellationToken ct = default)
    {
        _ = await _bookRepository.GetByIdAsync(bookId, ct)
            ?? throw new NotFoundException("Kitap", bookId);

        var profile = await _voiceRepository.GetByIdAsync(voiceProfileId, ct)
            ?? throw new NotFoundException("Ses profili", voiceProfileId);

        if (profile.Durum != SesProfiliDurumu.Hazir)
        {
            throw new BusinessException("Seçilen ses profili henüz kullanıma hazır değil.");
        }

        var chapters = await _chapterRepository.GetForSynthesisAsync(bookId, chapterIds, sadeceEksikler, ct);
        var totalWords = chapters.Sum(c => c.KelimeSayisi);
        var (audioSec, gpuSec, costUsd) = _costEstimator.Estimate(totalWords, includeColdStart: totalWords > 0);

        var now = _clock.UtcNow;
        var jobsMonthUsd = await _jobRepository.SumCostForMonthAsync(now.Year, now.Month, ct);
        var voicesMonthGpuMs = await _voiceRepository.SumGpuMsForMonthAsync(now.Year, now.Month, ct);
        var voicesMonthUsd = _costEstimator.CostFromGpuMs(voicesMonthGpuMs);
        var spentThisMonth = Math.Round(jobsMonthUsd + voicesMonthUsd, 4);

        var monthlyBudget = _ttsOptions.MonthlyBudgetUsd;
        var remainingBudget = Math.Max(0m, Math.Round(monthlyBudget - spentThisMonth, 4));
        var budgetExceeded = monthlyBudget > 0m && (spentThisMonth + costUsd) > monthlyBudget;

        var editedCount = chapters.Count(c => c.SesMetinSurumu.HasValue);
        var cachedRatio = chapters.Count > 0
            ? (int)Math.Round((double)editedCount / chapters.Count * 50.0)
            : 0;

        return new SynthesisEstimateDto(
            chapters.Count,
            totalWords,
            audioSec,
            gpuSec,
            costUsd,
            spentThisMonth,
            monthlyBudget,
            remainingBudget,
            budgetExceeded,
            cachedRatio);
    }

    /// <inheritdoc />
    public async Task<SynthesisJobDto> StartAsync(
        Guid bookId,
        SynthesisStartRequest request,
        string traceId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _startValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            throw new BusinessException(message);
        }

        var book = await _bookRepository.GetByIdAsync(bookId, ct)
            ?? throw new NotFoundException("Kitap", bookId);

        if (book.Durum is KitapDurumu.Indiriliyor or KitapDurumu.Isleniyor or KitapDurumu.OcrGerekli)
        {
            throw new BusinessException("Kitap henüz metin işleme aşamasını tamamlamadığı için seslendirilemez.");
        }

        var profile = await _voiceRepository.GetByIdAsync(request.VoiceProfileId, ct)
            ?? throw new NotFoundException("Ses profili", request.VoiceProfileId);

        if (profile.Durum != SesProfiliDurumu.Hazir ||
            string.IsNullOrWhiteSpace(profile.ReferansSesYolu) ||
            string.IsNullOrWhiteSpace(profile.ReferansHash))
        {
            throw new BusinessException("Seçilen ses profili hazır durumda değil.");
        }

        var existingDbJob = await _jobRepository.GetActiveJobForBookAsync(bookId, ct);
        if (existingDbJob is not null)
        {
            throw new ConflictException("Bu kitap için zaten devam eden bir seslendirme var.", existingDbJob.Id);
        }

        var chapters = await _chapterRepository.GetForSynthesisAsync(
            bookId,
            request.ChapterIds,
            request.SadeceEksikler,
            ct);

        if (chapters.Count == 0)
        {
            throw new BusinessException("Seslendirilecek uygun veya eksik bölüm bulunamadı.");
        }

        var totalWords = chapters.Sum(c => c.KelimeSayisi);
        var (audioSec, _, estimatedCostUsd) = _costEstimator.Estimate(totalWords);

        var now = _clock.UtcNow;
        var jobsMonthUsd = await _jobRepository.SumCostForMonthAsync(now.Year, now.Month, ct);
        var voicesMonthGpuMs = await _voiceRepository.SumGpuMsForMonthAsync(now.Year, now.Month, ct);
        var spentThisMonth = jobsMonthUsd + _costEstimator.CostFromGpuMs(voicesMonthGpuMs);
        var monthlyBudget = _ttsOptions.MonthlyBudgetUsd;

        if (!request.Force && monthlyBudget > 0m && (spentThisMonth + estimatedCostUsd) > monthlyBudget)
        {
            throw new BusinessException(
                $"{ErrorCodes.SynthesisBudgetExceeded}: Bu işin tahmini maliyeti ({estimatedCostUsd:F2} USD) ile bu ayki toplam harcama ({spentThisMonth + estimatedCostUsd:F2} USD), aylık bütçe sınırını ({monthlyBudget:F2} USD) aşıyor. Devam etmek için onaylayın.");
        }

        var jobId = Guid.CreateVersion7();
        var lockKey = CacheKeys.ActiveJob(bookId);
        var lockAcquired = await _cache.TryAcquireLockAsync(lockKey, jobId.ToString("D"), ActiveJobLockTtl, ct);
        if (!lockAcquired)
        {
            var existingLockVal = await _cache.GetStringAsync(lockKey, ct);
            Guid? existingJobId = Guid.TryParse(existingLockVal, out var parsed) ? parsed : null;
            throw new ConflictException("Bu kitap için zaten devam eden bir seslendirme var.", existingJobId);
        }

        var previousBookStatus = book.Durum;
        var previousChapterStatuses = chapters.ToDictionary(c => c.Id, c => c.SesDurumu);

        var engineOrderList = _ttsOptions.EngineOrder.Length > 0
            ? _ttsOptions.EngineOrder
            : ["voxcpm2_modal"];

        var jobSettings = new SynthesisJobSettings
        {
            CumleArasiMs = request.CumleArasiMs ?? _defaultsOptions.CumleArasiMs,
            ParagrafArasiMs = request.ParagrafArasiMs ?? _defaultsOptions.ParagrafArasiMs,
            BaslikSonrasiMs = _defaultsOptions.BaslikSonrasiMs,
            DiyalogMs = _defaultsOptions.DiyalogMs,
            BolumBasligiOku = _defaultsOptions.BolumBasligiOku,
            OpusBitrateKbps = _defaultsOptions.OpusBitrateKbps,
            Mp3BitrateKbps = _defaultsOptions.Mp3BitrateKbps,
            HedefLufs = _defaultsOptions.HedefLufs,
            SadeceEksikler = request.SadeceEksikler
        };

        var job = new SynthesisJob
        {
            Id = jobId,
            BookId = bookId,
            VoiceProfileId = profile.Id,
            MotorSirasi = string.Join(",", engineOrderList),
            Durum = SentezIsDurumu.Kuyrukta,
            ToplamBolum = chapters.Count,
            TamamlananBolum = 0,
            HataliBolum = 0,
            Yuzde = 0,
            Mesaj = "Seslendirme kuyruğa alındı.",
            TahminiSureSn = audioSec,
            TahminiMaliyetUsd = 0m,
            Ayarlar = jobSettings,
            TraceId = traceId,
            OlusturmaTarihi = now.UtcDateTime,
            SonIlerlemeZamani = now.UtcDateTime
        };

        var jobChapters = chapters
            .Select((c, idx) => new SynthesisJobChapter
            {
                JobId = jobId,
                ChapterId = c.Id,
                Sira = idx + 1,
                Durum = BolumSesDurumu.Kuyrukta,
                GpuMs = 0
            })
            .ToList();

        await _jobRepository.ExecuteInTransactionAsync(async token =>
        {
            await _jobRepository.AddAsync(job, token);
            await _jobRepository.AddJobChaptersAsync(jobChapters, token);

            foreach (var chapter in chapters)
            {
                chapter.SesDurumu = BolumSesDurumu.Kuyrukta;
                chapter.HataMesaji = null;
                _chapterRepository.Update(chapter);
            }

            book.Durum = KitapDurumu.Seslendiriliyor;
            book.HataMesaji = null;
            _bookRepository.Update(book);

            await _jobRepository.SaveChangesAsync(token);
        }, ct);

        IReadOnlyList<SynthesisInputPronunciation> pronunciationEntries = [];
        if (_pronunciationService is not null)
        {
            var allPronunciations = await _pronunciationService.GetAllAsync(ct);
            pronunciationEntries = allPronunciations
                .Where(p => p.AktifMi)
                .Select(p => new SynthesisInputPronunciation(p.Kelime, p.Okunus, p.BuyukKucukDuyarli))
                .ToList();
        }

        var inputPayload = new SynthesisInputPayload(
            jobId,
            new SynthesisInputBook(book.Id, book.Baslik, book.Yazar, book.KapakYolu),
            new SynthesisInputVoiceProfile(
                profile.Id,
                profile.Ad,
                profile.ReferansSesYolu,
                profile.ReferansHash,
                profile.ReferansMetin,
                profile.Parametreler),
            engineOrderList,
            new SynthesisInputSettings(
                jobSettings.CumleArasiMs,
                jobSettings.ParagrafArasiMs,
                jobSettings.BaslikSonrasiMs,
                jobSettings.DiyalogMs,
                jobSettings.BolumBasligiOku,
                jobSettings.OpusBitrateKbps,
                jobSettings.Mp3BitrateKbps,
                jobSettings.HedefLufs),
            pronunciationEntries,
            chapters
                .Select(c => new SynthesisInputChapter(
                    c.Id,
                    c.SiraNo,
                    c.Baslik,
                    c.MetinSurumu,
                    c.TemizMetin,
                    c.SesMetinSurumu))
                .ToList());

        var inputKey = StorageKeys.SynthesisInput(jobId);
        var pipelineCtx = new PipelineLogContext(bookId, jobId, null, traceId, "Api");
        var logId = await _logService.LogPipelineStartAsync(
            pipelineCtx,
            nameof(PipelineAsamasi.Sentez),
            JsonSerializer.Serialize(new { jobId, chapters = chapters.Count, totalWords, voiceProfileId = profile.Id }),
            ct);

        try
        {
            await _storage.UploadJsonAsync(inputKey, inputPayload, ct);
            await _publisher.PublishAsync(
                new SynthesisRequestedEvent
                {
                    TraceId = traceId,
                    JobId = jobId,
                    BookId = bookId,
                    GirdiYolu = inputKey
                },
                ct);

            await _logService.LogPipelineEndAsync(logId, JsonSerializer.Serialize(new { queued = true, inputKey }), ct);
        }
        catch (Exception ex)
        {
            await _logService.LogPipelineErrorAsync(logId, ex, ct);
            await _logService.LogFunctionErrorAsync(ErrorCodes.SynthesisStartError, ex, new { jobId, bookId }, traceId, ct: ct);

            await _cache.RemoveAsync(lockKey, ct);

            await _jobRepository.ExecuteInTransactionAsync(async token =>
            {
                job.Durum = SentezIsDurumu.Hata;
                job.HataMesaji = "Sentez işi kuyruğa gönderilemedi.";
                job.BitisZamani = _clock.UtcNow.UtcDateTime;
                _jobRepository.Update(job);

                foreach (var chapter in chapters)
                {
                    chapter.SesDurumu = previousChapterStatuses[chapter.Id];
                    _chapterRepository.Update(chapter);
                }

                book.Durum = previousBookStatus;
                _bookRepository.Update(book);

                await _jobRepository.SaveChangesAsync(token);
            }, ct);

            throw new ExternalServiceException("Sentez Kuyruğu", "Seslendirme işi başlatılamadı.", ex);
        }

        await _cache.RemoveAsync(CacheKeys.Book(bookId), ct);
        await _cache.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);
        await _notifications.BookStatusChangedAsync(bookId, KitapDurumu.Seslendiriliyor, ct);

        return MapToJobDto(job, book.Baslik, profile.Ad);
    }

    /// <inheritdoc />
    public async Task CancelAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetWithDetailsAsync(jobId, asNoTracking: false, ct)
            ?? throw new NotFoundException("Sentez işi", jobId);

        if (job.Durum is not (SentezIsDurumu.Kuyrukta or SentezIsDurumu.Calisiyor))
        {
            throw new BusinessException("Yalnızca kuyrukta veya çalışmakta olan bir seslendirme işi iptal edilebilir.");
        }

        await _cache.SetStringAsync(CacheKeys.JobCancel(jobId), "1", ActiveJobLockTtl, ct);
        await _cache.RemoveAsync(CacheKeys.ActiveJob(job.BookId), ct);

        var now = _clock.UtcNow.UtcDateTime;
        job.Durum = SentezIsDurumu.IptalEdildi;
        job.BitisZamani = now;
        job.SonIlerlemeZamani = now;
        job.Mesaj = "Kullanıcı tarafından iptal edildi.";

        foreach (var jc in job.Bolumler)
        {
            if (jc.Durum is BolumSesDurumu.Kuyrukta or BolumSesDurumu.Seslendiriliyor)
            {
                var ch = jc.Chapter;
                var restoredStatus = ch is not null ? RestoreChapterStatus(ch) : BolumSesDurumu.Yok;
                jc.Durum = restoredStatus;
                if (ch is not null)
                {
                    ch.SesDurumu = restoredStatus;
                    _chapterRepository.Update(ch);
                }
            }
        }

        await _jobRepository.SaveChangesAsync(ct);

        var newBookStatus = await ResolveAndSaveBookStatusAsync(job.BookId, ct);

        await _cache.RemoveAsync(CacheKeys.Book(job.BookId), ct);
        await _cache.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);
        await _notifications.BookStatusChangedAsync(job.BookId, newBookStatus, ct);
        await _notifications.SynthesisCompletedAsync(
            job.Id,
            job.BookId,
            nameof(SentezIsDurumu.IptalEdildi),
            job.TamamlananBolum,
            job.HataliBolum,
            job.TahminiMaliyetUsd,
            ct);
    }

    /// <inheritdoc />
    public async Task<SynthesisJobDto> RetryAsync(Guid jobId, string traceId, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetWithDetailsAsync(jobId, asNoTracking: true, ct)
            ?? throw new NotFoundException("Sentez işi", jobId);

        if (job.Durum is not (SentezIsDurumu.Hata or SentezIsDurumu.IptalEdildi or SentezIsDurumu.KismenTamamlandi))
        {
            throw new BusinessException("Yalnızca hatalı, iptal edilmiş veya kısmen tamamlanmış işler yeniden denenebilir.");
        }

        var missingChapterIds = job.Bolumler
            .OrderBy(jc => jc.Sira)
            .Where(jc => jc.Durum != BolumSesDurumu.Hazir)
            .Select(jc => jc.ChapterId)
            .ToList();

        var startRequest = new SynthesisStartRequest(
            job.VoiceProfileId,
            missingChapterIds.Count > 0 ? missingChapterIds : null,
            SadeceEksikler: true,
            Force: true);

        return await StartAsync(job.BookId, startRequest, traceId, ct);
    }

    /// <inheritdoc />
    public async Task<PagedResult<SynthesisJobDto>> GetListAsync(
        SentezIsDurumu? durum,
        PageQuery page,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        var sayfa = Math.Max(1, page.Sayfa);
        var boyut = Math.Clamp(page.Boyut, 1, 100);
        var (items, total) = await _jobRepository.GetPagedListAsync(durum, sayfa, boyut, ct);

        var dtos = items
            .Select(j => MapToJobDto(
                j,
                j.Book?.Baslik ?? string.Empty,
                j.VoiceProfile?.Ad ?? string.Empty))
            .ToList();

        return new PagedResult<SynthesisJobDto>(dtos, total, sayfa, boyut);
    }

    /// <inheritdoc />
    public async Task<SynthesisJobDetailDto> GetByIdAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetWithDetailsAsync(jobId, asNoTracking: true, ct)
            ?? throw new NotFoundException("Sentez işi", jobId);

        var jobDto = MapToJobDto(
            job,
            job.Book?.Baslik ?? string.Empty,
            job.VoiceProfile?.Ad ?? string.Empty);

        var chapters = job.Bolumler
            .OrderBy(jc => jc.Sira)
            .Select(jc => new SynthesisJobChapterDto(
                jc.ChapterId,
                jc.Sira,
                jc.Chapter?.SiraNo ?? jc.Sira,
                jc.Chapter?.Baslik ?? $"Bölüm {jc.Sira}",
                jc.Durum,
                jc.GpuMs,
                jc.Chapter?.SureSn,
                jc.HataMesaji))
            .ToList();

        return new SynthesisJobDetailDto(jobDto, job.Ayarlar, chapters);
    }

    /// <inheritdoc />
    public async Task HandleStageChangedAsync(PipelineStageChangedEvent stageEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stageEvent);
        if (!stageEvent.JobId.HasValue)
        {
            return;
        }

        var job = await _jobRepository.GetByIdAsync(stageEvent.JobId.Value, ct);
        if (job is null ||
            job.Durum is SentezIsDurumu.Tamamlandi or SentezIsDurumu.IptalEdildi or SentezIsDurumu.Hata)
        {
            return;
        }

        var now = _clock.UtcNow.UtcDateTime;
        if (job.Durum == SentezIsDurumu.Kuyrukta)
        {
            job.Durum = SentezIsDurumu.Calisiyor;
            job.BaslangicZamani ??= now;
        }

        job.Yuzde = Math.Clamp(stageEvent.Yuzde, 0, 100);
        job.Mesaj = stageEvent.Mesaj;
        if (stageEvent.ChapterId.HasValue)
        {
            job.MevcutBolumId = stageEvent.ChapterId.Value;
        }

        job.SonIlerlemeZamani = now;
        _jobRepository.Update(job);

        if (stageEvent.ChapterId.HasValue)
        {
            var chapter = await _chapterRepository.GetByIdAsync(stageEvent.ChapterId.Value, ct);
            if (chapter is not null && chapter.SesDurumu == BolumSesDurumu.Kuyrukta)
            {
                chapter.SesDurumu = BolumSesDurumu.Seslendiriliyor;
                _chapterRepository.Update(chapter);
            }

            var jobChapter = await _jobRepository.GetJobChapterAsync(job.Id, stageEvent.ChapterId.Value, ct);
            if (jobChapter is not null && jobChapter.Durum == BolumSesDurumu.Kuyrukta)
            {
                jobChapter.Durum = BolumSesDurumu.Seslendiriliyor;
            }
        }

        await _jobRepository.SaveChangesAsync(ct);

        int bolumYuzde = stageEvent.Yuzde;
        if (stageEvent.Detay is { } detay &&
            detay.TryGetValue("bolumYuzde", out var rawBolumYuzde) &&
            rawBolumYuzde is not null)
        {
            if (rawBolumYuzde is JsonElement je && je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var parsedJe))
            {
                bolumYuzde = Math.Clamp(parsedJe, 0, 100);
            }
            else if (int.TryParse(rawBolumYuzde.ToString(), out var parsedInt))
            {
                bolumYuzde = Math.Clamp(parsedInt, 0, 100);
            }
        }

        double? estimatedRemainingSec = null;
        if (job.BaslangicZamani.HasValue && job.Yuzde > 0 && job.Yuzde < 100)
        {
            var elapsedSec = Math.Max(1.0, (now - job.BaslangicZamani.Value).TotalSeconds);
            estimatedRemainingSec = Math.Round(elapsedSec * (100.0 - job.Yuzde) / job.Yuzde, 1);
        }

        await _notifications.SynthesisProgressAsync(
            job.Id,
            job.BookId,
            job.MevcutBolumId,
            job.Yuzde,
            bolumYuzde,
            job.TamamlananBolum,
            job.ToplamBolum,
            job.Mesaj ?? string.Empty,
            job.GpuMs,
            estimatedRemainingSec,
            ct);
    }

    /// <inheritdoc />
    public async Task HandleChapterSynthesizedAsync(
        ChapterSynthesizedEvent chapterEvent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chapterEvent);

        var chapter = await _chapterRepository.GetByIdAsync(chapterEvent.ChapterId, ct);
        if (chapter is null)
        {
            return;
        }

        var job = await _jobRepository.GetByIdAsync(chapterEvent.JobId, ct);
        var jobChapter = await _jobRepository.GetJobChapterAsync(chapterEvent.JobId, chapterEvent.ChapterId, ct);

        // İdempotency kontrolü: aynı sürüm ve aynı dosya yolu zaten işlendiyse çık
        if (jobChapter is { Durum: BolumSesDurumu.Hazir } &&
            chapter.SesMetinSurumu == chapterEvent.MetinSurumu &&
            string.Equals(chapter.OpusYolu, chapterEvent.OpusYolu, StringComparison.Ordinal))
        {
            return;
        }

        var oldVersion = chapter.SesMetinSurumu;
        var isUpToDate = chapterEvent.MetinSurumu == chapter.MetinSurumu;
        chapter.SesDurumu = isUpToDate ? BolumSesDurumu.Hazir : BolumSesDurumu.GuncelDegil;
        chapter.OpusYolu = chapterEvent.OpusYolu;
        chapter.Mp3Yolu = chapterEvent.Mp3Yolu;
        chapter.ZamanDamgasiYolu = chapterEvent.ZamanDamgasiYolu;
        chapter.OpusBoyutu = chapterEvent.OpusBoyutu;
        chapter.Mp3Boyutu = chapterEvent.Mp3Boyutu;
        chapter.SureSn = chapterEvent.SureSn;
        chapter.CumleSayisi = chapterEvent.CumleSayisi;
        chapter.SesMetinSurumu = chapterEvent.MetinSurumu;
        chapter.SesProfiliId = job?.VoiceProfileId ?? chapter.SesProfiliId;
        chapter.SesMotoru = ParseTtsMotoru(chapterEvent.Motor);
        chapter.GpuMs += chapterEvent.GpuMs;
        chapter.HataMesaji = null;
        _chapterRepository.Update(chapter);

        var now = _clock.UtcNow.UtcDateTime;
        if (jobChapter is not null)
        {
            jobChapter.Durum = BolumSesDurumu.Hazir;
            jobChapter.GpuMs = chapterEvent.GpuMs;
            jobChapter.HataMesaji = null;
        }

        if (job is not null)
        {
            job.TamamlananBolum = Math.Min(job.ToplamBolum, job.TamamlananBolum + 1);
            job.GpuMs += chapterEvent.GpuMs;
            job.TahminiMaliyetUsd = _costEstimator.CostFromGpuMs(job.GpuMs);
            job.UretilenSesSn = Math.Round(job.UretilenSesSn + chapterEvent.SureSn, 2);
            job.OnbellektenParca += chapterEvent.OnbellekParca;
            job.UretilenParca += chapterEvent.UretilenParca;
            job.ToplamParca = job.OnbellektenParca + job.UretilenParca;
            job.KullanilanMotorlar = AppendEngine(job.KullanilanMotorlar, chapterEvent.Motor);
            job.SonIlerlemeZamani = now;

            if (job.ToplamBolum > 0)
            {
                job.Yuzde = Math.Clamp((int)Math.Round((double)job.TamamlananBolum / job.ToplamBolum * 100.0), 0, 100);
            }

            _jobRepository.Update(job);
        }

        await _chapterRepository.SaveChangesAsync(ct);
        await _chapterRepository.RecalculateBookAudioDurationAsync(chapterEvent.BookId, ct);

        if (oldVersion.HasValue && oldVersion.Value != chapterEvent.MetinSurumu)
        {
            var oldPrefix = $"books/{chapterEvent.BookId:D}/audio/{chapterEvent.ChapterId:D}/v{oldVersion.Value}/";
            await _storage.DeleteByPrefixAsync(oldPrefix, ct);
        }

        await _storage.DeleteByPrefixAsync(StorageKeys.BookExportPrefix(chapterEvent.BookId), ct);

        await _cache.RemoveAsync(CacheKeys.ChapterAudioInfo(chapter.Id, "opus"), ct);
        await _cache.RemoveAsync(CacheKeys.ChapterAudioInfo(chapter.Id, "mp3"), ct);
        await _cache.RemoveAsync(CacheKeys.ChapterTimestamps(chapter.Id), ct);
        await _cache.RemoveAsync(CacheKeys.Book(chapterEvent.BookId), ct);
        await _cache.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);

        await _notifications.ChapterReadyAsync(
            chapterEvent.JobId,
            chapterEvent.BookId,
            chapterEvent.ChapterId,
            chapterEvent.SureSn,
            chapterEvent.Motor,
            ct);
    }

    /// <inheritdoc />
    public async Task HandleCompletedAsync(
        SynthesisCompletedEvent completedEvent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(completedEvent);

        var job = await _jobRepository.GetWithDetailsAsync(completedEvent.JobId, asNoTracking: false, ct);
        if (job is null)
        {
            return;
        }

        var now = _clock.UtcNow.UtcDateTime;
        SentezIsDurumu finalStatus;
        if (completedEvent.IptalEdildi || job.Durum == SentezIsDurumu.IptalEdildi)
        {
            finalStatus = SentezIsDurumu.IptalEdildi;
        }
        else if (completedEvent.HataliBolum > 0 && completedEvent.TamamlananBolum > 0)
        {
            finalStatus = SentezIsDurumu.KismenTamamlandi;
        }
        else if (completedEvent.TamamlananBolum == 0 && completedEvent.HataliBolum > 0)
        {
            finalStatus = SentezIsDurumu.Hata;
        }
        else
        {
            finalStatus = SentezIsDurumu.Tamamlandi;
        }

        job.Durum = finalStatus;
        job.BitisZamani = now;
        job.SonIlerlemeZamani = now;
        if (finalStatus == SentezIsDurumu.Tamamlandi)
        {
            job.Yuzde = 100;
            job.Mesaj = "Seslendirme tamamlandı.";
        }
        else if (finalStatus == SentezIsDurumu.KismenTamamlandi)
        {
            job.Mesaj = $"Seslendirme kısmen tamamlandı ({job.TamamlananBolum}/{job.ToplamBolum} bölüm).";
        }
        else if (finalStatus == SentezIsDurumu.IptalEdildi)
        {
            job.Mesaj = "Seslendirme iptal edildi.";
        }

        if (!string.IsNullOrWhiteSpace(completedEvent.KullanilanMotorlar))
        {
            job.KullanilanMotorlar = completedEvent.KullanilanMotorlar;
        }

        foreach (var jc in job.Bolumler)
        {
            if (jc.Durum is BolumSesDurumu.Kuyrukta or BolumSesDurumu.Seslendiriliyor)
            {
                var ch = jc.Chapter;
                var restored = ch is not null ? RestoreChapterStatus(ch) : BolumSesDurumu.Yok;
                jc.Durum = restored;
                if (ch is not null)
                {
                    ch.SesDurumu = restored;
                    _chapterRepository.Update(ch);
                }
            }
        }

        _jobRepository.Update(job);
        await _jobRepository.SaveChangesAsync(ct);

        var newBookStatus = await ResolveAndSaveBookStatusAsync(job.BookId, ct);

        await _cache.RemoveAsync(CacheKeys.ActiveJob(job.BookId), ct);
        await _cache.RemoveAsync(CacheKeys.JobCancel(job.Id), ct);
        await _cache.RemoveAsync(CacheKeys.Book(job.BookId), ct);
        await _cache.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);

        await _notifications.BookStatusChangedAsync(job.BookId, newBookStatus, ct);
        await _notifications.SynthesisCompletedAsync(
            job.Id,
            job.BookId,
            finalStatus.ToString(),
            job.TamamlananBolum,
            job.HataliBolum,
            job.TahminiMaliyetUsd,
            ct);
    }

    /// <inheritdoc />
    public async Task HandleErrorAsync(PipelineErrorEvent errorEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(errorEvent);
        if (!errorEvent.JobId.HasValue)
        {
            return;
        }

        var job = await _jobRepository.GetByIdAsync(errorEvent.JobId.Value, ct);
        if (job is null)
        {
            return;
        }

        if (errorEvent.ChapterId.HasValue)
        {
            var chapter = await _chapterRepository.GetByIdAsync(errorEvent.ChapterId.Value, ct);
            if (chapter is not null)
            {
                chapter.SesDurumu = BolumSesDurumu.Hata;
                chapter.HataMesaji = errorEvent.HataMesaji;
                _chapterRepository.Update(chapter);
            }

            var jobChapter = await _jobRepository.GetJobChapterAsync(job.Id, errorEvent.ChapterId.Value, ct);
            if (jobChapter is not null && jobChapter.Durum != BolumSesDurumu.Hata)
            {
                jobChapter.Durum = BolumSesDurumu.Hata;
                jobChapter.HataMesaji = errorEvent.HataMesaji;
                job.HataliBolum++;
            }

            job.SonIlerlemeZamani = _clock.UtcNow.UtcDateTime;
            _jobRepository.Update(job);
            await _jobRepository.SaveChangesAsync(ct);

            await _cache.RemoveAsync(CacheKeys.Book(job.BookId), ct);
            await _notifications.PipelineErrorAsync(
                job.BookId,
                job.Id,
                null,
                errorEvent.Asama,
                errorEvent.HataMesaji,
                errorEvent.Kurtarilabilir,
                ct);
            return;
        }

        if (!errorEvent.Kurtarilabilir)
        {
            await MarkJobFailedAsync(job.Id, errorEvent.HataMesaji, ct);
        }
    }

    /// <inheritdoc />
    public async Task MarkJobFailedAsync(Guid jobId, string reason, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetWithDetailsAsync(jobId, asNoTracking: false, ct);
        if (job is null ||
            job.Durum is SentezIsDurumu.Tamamlandi or SentezIsDurumu.IptalEdildi or SentezIsDurumu.Hata)
        {
            return;
        }

        var now = _clock.UtcNow.UtcDateTime;
        job.Durum = SentezIsDurumu.Hata;
        job.HataMesaji = reason;
        job.Mesaj = reason;
        job.BitisZamani = now;
        job.SonIlerlemeZamani = now;

        foreach (var jc in job.Bolumler)
        {
            if (jc.Durum is BolumSesDurumu.Kuyrukta or BolumSesDurumu.Seslendiriliyor)
            {
                var ch = jc.Chapter;
                var restored = ch is not null ? RestoreChapterStatus(ch) : BolumSesDurumu.Yok;
                jc.Durum = restored;
                if (ch is not null)
                {
                    ch.SesDurumu = restored;
                    _chapterRepository.Update(ch);
                }
            }
        }

        _jobRepository.Update(job);
        await _jobRepository.SaveChangesAsync(ct);

        var newBookStatus = await ResolveAndSaveBookStatusAsync(job.BookId, ct);

        await _cache.RemoveAsync(CacheKeys.ActiveJob(job.BookId), ct);
        await _cache.RemoveAsync(CacheKeys.Book(job.BookId), ct);
        await _cache.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);

        await _notifications.BookStatusChangedAsync(job.BookId, newBookStatus, ct);
        await _notifications.PipelineErrorAsync(
            job.BookId,
            job.Id,
            null,
            nameof(PipelineAsamasi.Sentez),
            reason,
            kurtarilabilir: true,
            ct);
    }

    private async Task<KitapDurumu> ResolveAndSaveBookStatusAsync(Guid bookId, CancellationToken ct)
    {
        var book = await _bookRepository.GetByIdAsync(bookId, ct);
        if (book is null)
        {
            return KitapDurumu.OnizlemeBekliyor;
        }

        var allChapters = await _chapterRepository.GetSummariesAsync(bookId, ct);
        var included = allChapters.Where(c => c.DahilMi).ToList();

        KitapDurumu nextStatus;
        if (included.Count > 0 && included.All(c => c.SesDurumu == BolumSesDurumu.Hazir))
        {
            nextStatus = KitapDurumu.Hazir;
        }
        else if (allChapters.Any(c => c.SesDurumu is BolumSesDurumu.Hazir or BolumSesDurumu.GuncelDegil))
        {
            nextStatus = KitapDurumu.KismenHazir;
        }
        else
        {
            nextStatus = KitapDurumu.OnizlemeBekliyor;
        }

        book.Durum = nextStatus;
        _bookRepository.Update(book);
        await _bookRepository.SaveChangesAsync(ct);
        return nextStatus;
    }

    private static BolumSesDurumu RestoreChapterStatus(BookChapter chapter)
    {
        if (!chapter.SesMetinSurumu.HasValue || string.IsNullOrWhiteSpace(chapter.OpusYolu))
        {
            return BolumSesDurumu.Yok;
        }

        return chapter.SesMetinSurumu.Value == chapter.MetinSurumu
            ? BolumSesDurumu.Hazir
            : BolumSesDurumu.GuncelDegil;
    }

    private static TtsMotoru ParseTtsMotoru(string? motor)
    {
        if (string.IsNullOrWhiteSpace(motor))
        {
            return TtsMotoru.VoxCpm2;
        }

        var lower = motor.ToLowerInvariant();
        if (lower.Contains("xtts", StringComparison.Ordinal))
        {
            return TtsMotoru.Xtts2;
        }

        if (lower.Contains("mock", StringComparison.Ordinal))
        {
            return TtsMotoru.Mock;
        }

        return TtsMotoru.VoxCpm2;
    }

    private static string AppendEngine(string? existing, string current)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return existing ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(existing))
        {
            return current.Trim();
        }

        var parts = existing
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (!parts.Contains(current.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            parts.Add(current.Trim());
        }

        return string.Join(",", parts);
    }

    private static SynthesisJobDto MapToJobDto(SynthesisJob job, string bookTitle, string voiceName) =>
        new(
            job.Id,
            job.BookId,
            bookTitle,
            job.VoiceProfileId,
            voiceName,
            job.Durum,
            job.Yuzde,
            job.ToplamBolum,
            job.TamamlananBolum,
            job.HataliBolum,
            job.MevcutBolumId,
            job.Mesaj,
            job.MotorSirasi,
            job.KullanilanMotorlar,
            job.GpuMs,
            job.TahminiMaliyetUsd,
            job.TahminiSureSn,
            job.UretilenSesSn,
            job.OnbellektenParca,
            job.UretilenParca,
            job.HataMesaji,
            job.OlusturmaTarihi,
            job.BaslangicZamani,
            job.BitisZamani);
}
