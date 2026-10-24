using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Options;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Book;
using SesliKitap.Business.DTOs.Chapter;
using SesliKitap.Business.DTOs.Common;
using SesliKitap.Business.DTOs.Logs;
using SesliKitap.Business.DTOs.Synthesis;
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
/// <see cref="IBookService"/> sözleşmesinin kitap yükleme, listeleme, güncelleme, silme ve yeniden işleme uygulaması.
/// </summary>
public sealed class BookManager : IBookService
{
    private readonly IBookRepository _bookRepository;
    private readonly IChapterRepository _chapterRepository;
    private readonly ISynthesisJobRepository? _jobRepository;
    private readonly IFileStorageService _storage;
    private readonly ICacheService _cache;
    private readonly IEventPublisher _eventPublisher;
    private readonly INotificationService _notifications;
    private readonly ILogService _logService;
    private readonly IValidator<BookUploadRequest> _uploadValidator;
    private readonly IValidator<BookUpdateRequest> _updateValidator;
    private readonly CacheOptions _cacheOptions;

    /// <summary>
    /// <see cref="BookManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public BookManager(
        IBookRepository bookRepository,
        IChapterRepository chapterRepository,
        IFileStorageService storage,
        ICacheService cache,
        IEventPublisher eventPublisher,
        INotificationService notifications,
        ILogService logService,
        IValidator<BookUploadRequest> uploadValidator,
        IValidator<BookUpdateRequest> updateValidator,
        IOptions<CacheOptions> cacheOptions,
        ISynthesisJobRepository? jobRepository = null)
    {
        _bookRepository = bookRepository;
        _chapterRepository = chapterRepository;
        _jobRepository = jobRepository;
        _storage = storage;
        _cache = cache;
        _eventPublisher = eventPublisher;
        _notifications = notifications;
        _logService = logService;
        _uploadValidator = uploadValidator;
        _updateValidator = updateValidator;
        _cacheOptions = cacheOptions.Value;
    }

    /// <inheritdoc />
    public async Task<PagedResult<BookListDto>> GetListAsync(BookQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sayfa = Math.Max(1, query.Sayfa);
        var boyut = Math.Clamp(query.Boyut, 1, 100);
        var siralama = string.IsNullOrWhiteSpace(query.Siralama) ? "eklenme" : query.Siralama.Trim();
        var filterRaw = $"{query.Arama?.Trim()}|{query.Durum}|{siralama}|{sayfa}|{boyut}";
        var cacheKey = CacheKeys.BookList(HashHelper.Sha1Hex(filterRaw));

        var result = await _cache.GetOrSetAsync(
            cacheKey,
            async token =>
            {
                var (rows, toplam) = await _bookRepository.GetPagedListAsync(
                    query.Arama,
                    query.Durum,
                    siralama,
                    sayfa,
                    boyut,
                    token);

                var dtos = rows.Select(MapToListDto).ToList();
                return new PagedResult<BookListDto>(dtos, toplam, sayfa, boyut);
            },
            TimeSpan.FromSeconds(_cacheOptions.BookListSeconds),
            ct);

        return result!;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookListDto>> GetContinueListeningAsync(int count, CancellationToken ct = default)
    {
        var rows = await _bookRepository.GetContinueListeningAsync(count, ct);
        return rows.Select(MapToListDto).ToList();
    }

    /// <inheritdoc />
    public async Task<BookDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cached = await _cache.GetAsync<BookDetailDto>(CacheKeys.Book(id), ct);
        if (cached is not null)
        {
            return cached;
        }

        var detail = await BuildDetailDtoAsync(id, ct);
        await _cache.SetAsync(CacheKeys.Book(id), detail, TimeSpan.FromMinutes(_cacheOptions.BookDetailMinutes), ct);
        return detail;
    }

    /// <inheritdoc />
    public async Task<BookDetailDto> UploadAsync(BookUploadRequest request, string traceId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _uploadValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            throw new BusinessException(message);
        }

        var ext = Path.GetExtension(request.Dosya.FileName).ToLowerInvariant();
        DosyaFormati? detectedFormat;
        string fileHash;

        await using (var signatureStream = request.Dosya.OpenReadStream())
        {
            detectedFormat = await FileSignatureValidator.DetectBookFormatAsync(signatureStream, ext, ct);
        }

        if (detectedFormat is null)
        {
            throw new BusinessException("Dosya içeriği uzantıyla uyuşmuyor.");
        }

        await using (var hashStream = request.Dosya.OpenReadStream())
        {
            fileHash = await HashHelper.ComputeSha256Async(hashStream, ct);
        }

        var existing = await _bookRepository.FindByHashAsync(fileHash, ct);
        if (existing is not null)
        {
            await _logService.LogFunctionWarningAsync(
                ErrorCodes.BookDuplicate,
                "Aynı SHA-256 özetine sahip kitap zaten kütüphanede mevcut.",
                new { ExistingBookId = existing.Id, FileHash = fileHash },
                traceId,
                ct: ct);

            throw new ConflictException("Bu kitap zaten kütüphanenizde.", existing.Id);
        }

        var bookId = Guid.CreateVersion7();
        var extNoDot = ContentTypeMap.ToExtension(detectedFormat.Value);
        var sourceKey = StorageKeys.BookSource(bookId, extNoDot);
        var contentType = ContentTypeMap.GetContentType(extNoDot);

        await using (var uploadStream = request.Dosya.OpenReadStream())
        {
            await _storage.UploadAsync(sourceKey, uploadStream, request.Dosya.Length, contentType, ct);
        }

        var derivedTitle = !string.IsNullOrWhiteSpace(request.Baslik)
            ? request.Baslik.Trim()
            : DeriveTitleFromFileName(request.Dosya.FileName);

        var book = new Book
        {
            Id = bookId,
            Baslik = derivedTitle,
            Yazar = string.IsNullOrWhiteSpace(request.Yazar) ? null : request.Yazar.Trim(),
            Dil = "tr",
            KaynakTipi = KaynakTipi.Yukleme,
            DosyaFormati = detectedFormat.Value,
            OrijinalDosyaYolu = sourceKey,
            OrijinalDosyaAdi = Path.GetFileName(request.Dosya.FileName),
            DosyaBoyutu = request.Dosya.Length,
            DosyaHash = fileHash,
            Durum = KitapDurumu.Isleniyor
        };

        await _bookRepository.AddAsync(book, ct);
        await _bookRepository.SaveChangesAsync(ct);

        var pipelineCtx = new PipelineLogContext(bookId, null, null, traceId, "Api");
        var logId = await _logService.LogPipelineStartAsync(
            pipelineCtx,
            nameof(PipelineAsamasi.Indirme),
            JsonSerializer.Serialize(new { sourceKey, format = extNoDot, size = book.DosyaBoyutu }),
            ct);

        try
        {
            var importEvent = new BookImportRequestedEvent
            {
                TraceId = traceId,
                BookId = bookId,
                KaynakTipi = KaynakTipi.Yukleme,
                OrijinalDosyaYolu = sourceKey,
                DosyaFormati = detectedFormat.Value,
                BeklenenFormat = extNoDot
            };

            await _eventPublisher.PublishAsync(importEvent, ct);
            await _logService.LogPipelineEndAsync(logId, JsonSerializer.Serialize(new { queued = true }), ct);
        }
        catch (Exception ex)
        {
            await _logService.LogPipelineErrorAsync(logId, ex, ct);
            await _logService.LogFunctionErrorAsync(ErrorCodes.BookUploadError, ex, new { bookId, sourceKey }, traceId, ct: ct);
            await _bookRepository.UpdateStatusAsync(
                bookId,
                KitapDurumu.Hata,
                "Kitap işleme kuyruğuna gönderilemedi. 'Yeniden işle' ile tekrar deneyebilirsiniz.",
                ct);

            throw new ExternalServiceException("RabbitMQ", "Kitap işleme kuyruğuna gönderilemedi.", ex);
        }

        await _cache.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);
        await _notifications.BookStatusChangedAsync(bookId, KitapDurumu.Isleniyor, ct);

        return await BuildDetailDtoAsync(bookId, ct);
    }

    /// <inheritdoc />
    public async Task<BookDetailDto> UpdateAsync(Guid id, BookUpdateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            throw new BusinessException(message);
        }

        var book = await _bookRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Kitap", id);

        book.Baslik = request.Baslik.Trim();
        book.Yazar = string.IsNullOrWhiteSpace(request.Yazar) ? null : request.Yazar.Trim();
        book.Aciklama = string.IsNullOrWhiteSpace(request.Aciklama) ? null : request.Aciklama.Trim();
        book.VarsayilanSesProfiliId = request.VarsayilanSesProfiliId;

        _bookRepository.Update(book);
        await _bookRepository.SaveChangesAsync(ct);

        await _cache.RemoveAsync(CacheKeys.Book(id), ct);
        await _cache.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);

        return await BuildDetailDtoAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Kitap", id);

        // Aktif seslendirme işi varsa iptal bayrağı koy ve kilidi temizle
        var activeJobIdStr = await _cache.GetStringAsync(CacheKeys.ActiveJob(id), ct);
        if (Guid.TryParse(activeJobIdStr, out var activeJobId))
        {
            await _cache.SetStringAsync(CacheKeys.JobCancel(activeJobId), "1", TimeSpan.FromHours(24), ct);
            await _cache.RemoveAsync(CacheKeys.ActiveJob(id), ct);
        }

        _bookRepository.Delete(book);
        await _bookRepository.SaveChangesAsync(ct);

        await _cache.RemoveAsync(CacheKeys.Book(id), ct);
        await _cache.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);
    }

    /// <inheritdoc />
    public async Task ReprocessAsync(Guid id, bool onay, string traceId, CancellationToken ct = default)
    {
        if (!onay)
        {
            throw new BusinessException("Bölüm düzenlemeleri silinecek, onaylayın.");
        }

        var book = await _bookRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Kitap", id);

        var activeJob = await _cache.GetStringAsync(CacheKeys.ActiveJob(id), ct);
        if (!string.IsNullOrEmpty(activeJob) || book.Durum == KitapDurumu.Seslendiriliyor)
        {
            throw new ConflictException("Seslendirme devam ederken kitap yeniden işlenemez.");
        }

        if (string.IsNullOrEmpty(book.OrijinalDosyaYolu))
        {
            throw new BusinessException("Kitabın orijinal kaynak dosyası bulunamadı.");
        }

        await _storage.DeleteByPrefixAsync(StorageKeys.BookAudioPrefix(id), ct);

        await _bookRepository.ExecuteInTransactionAsync(async token =>
        {
            await _chapterRepository.DeleteByBookIdAsync(id, token);

            book.Durum = KitapDurumu.Isleniyor;
            book.HataMesaji = null;
            book.KelimeSayisi = 0;
            book.ToplamSureSn = 0;
            book.Metadata = null;

            _bookRepository.Update(book);
            await _bookRepository.SaveChangesAsync(token);
        }, ct);

        var extNoDot = ContentTypeMap.ToExtension(book.DosyaFormati);
        var importEvent = new BookImportRequestedEvent
        {
            TraceId = traceId,
            BookId = book.Id,
            KaynakTipi = book.KaynakTipi,
            OrijinalDosyaYolu = book.OrijinalDosyaYolu,
            DosyaFormati = book.DosyaFormati,
            BeklenenFormat = extNoDot
        };

        await _eventPublisher.PublishAsync(importEvent, ct);

        await _cache.RemoveAsync(CacheKeys.Book(id), ct);
        await _cache.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);
        await _notifications.BookStatusChangedAsync(id, KitapDurumu.Isleniyor, ct);
    }

    /// <inheritdoc />
    public async Task<(Stream Stream, string ContentType, string ETag)> OpenCoverAsync(Guid id, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Kitap", id);

        if (string.IsNullOrEmpty(book.KapakYolu))
        {
            throw new NotFoundException("Kitap kapağı", id);
        }

        var info = await _storage.GetInfoAsync(book.KapakYolu, ct)
            ?? throw new NotFoundException("Kitap kapağı", id);

        var ms = new MemoryStream((int)Math.Min(info.Size, 5 * 1024 * 1024));
        await _storage.CopyToAsync(book.KapakYolu, ms, 0, null, ct);
        ms.Position = 0;

        var etag = string.IsNullOrEmpty(info.ETag) ? $"\"{id:N}\"" : $"\"{info.ETag.Trim('"')}\"";
        return (ms, "image/jpeg", etag);
    }

    /// <inheritdoc />
    public async Task<(Stream Stream, string ContentType, string FileName)> OpenSourceAsync(Guid id, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Kitap", id);

        if (string.IsNullOrEmpty(book.OrijinalDosyaYolu))
        {
            throw new NotFoundException("Kaynak dosya", id);
        }

        var info = await _storage.GetInfoAsync(book.OrijinalDosyaYolu, ct)
            ?? throw new NotFoundException("Kaynak dosya", id);

        var ms = new MemoryStream((int)Math.Min(info.Size, 32 * 1024 * 1024));
        await _storage.CopyToAsync(book.OrijinalDosyaYolu, ms, 0, null, ct);
        ms.Position = 0;

        var ext = ContentTypeMap.ToExtension(book.DosyaFormati);
        var contentType = ContentTypeMap.GetContentType(ext);
        var fileName = !string.IsNullOrWhiteSpace(book.OrijinalDosyaAdi)
            ? book.OrijinalDosyaAdi
            : $"{TurkishTextHelper.Slugify(book.Baslik)}.{ext}";

        return (ms, contentType, fileName);
    }

    private async Task<BookDetailDto> BuildDetailDtoAsync(Guid id, CancellationToken ct)
    {
        var book = await _bookRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Kitap", id);

        var chapterRows = await _chapterRepository.GetSummariesAsync(id, ct);
        var chapters = chapterRows
            .Select(c => new ChapterSummaryDto(
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
                c.MetinSurumu))
            .ToList();

        JsonElement? statsElement = null;
        if (!string.IsNullOrWhiteSpace(book.Metadata))
        {
            try
            {
                using var doc = JsonDocument.Parse(book.Metadata);
                statsElement = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                // Bozuk metadata JSON'u detay yanıtını bozmasın
            }
        }

        SynthesisJobDto? activeJobDto = null;
        if (_jobRepository is not null)
        {
            var activeJob = await _jobRepository.GetActiveJobForBookAsync(id, ct);
            if (activeJob is not null)
            {
                activeJobDto = new SynthesisJobDto(
                    activeJob.Id,
                    activeJob.BookId,
                    book.Baslik,
                    activeJob.VoiceProfileId,
                    activeJob.VoiceProfile?.Ad ?? string.Empty,
                    activeJob.Durum,
                    activeJob.Yuzde,
                    activeJob.ToplamBolum,
                    activeJob.TamamlananBolum,
                    activeJob.HataliBolum,
                    activeJob.MevcutBolumId,
                    activeJob.Mesaj,
                    activeJob.MotorSirasi,
                    activeJob.KullanilanMotorlar,
                    activeJob.GpuMs,
                    activeJob.TahminiMaliyetUsd,
                    activeJob.TahminiSureSn,
                    activeJob.UretilenSesSn,
                    activeJob.OnbellektenParca,
                    activeJob.UretilenParca,
                    activeJob.HataMesaji,
                    activeJob.OlusturmaTarihi,
                    activeJob.BaslangicZamani,
                    activeJob.BitisZamani);
            }
        }

        return new BookDetailDto(
            book.Id,
            book.Baslik,
            book.Yazar,
            book.Aciklama,
            book.Dil,
            book.YayinYili,
            book.KaynakTipi,
            book.DosyaFormati,
            book.DosyaBoyutu,
            book.SayfaSayisi,
            book.KelimeSayisi,
            book.ToplamSureSn,
            book.Durum,
            book.HataMesaji,
            book.VarsayilanSesProfiliId,
            !string.IsNullOrEmpty(book.KapakYolu),
            chapters,
            statsElement,
            book.OlusturmaTarihi,
            book.SonDinlenmeTarihi,
            activeJobDto);
    }

    private static BookListDto MapToListDto(BookListRow r) =>
        new(
            r.Id,
            r.Baslik,
            r.Yazar,
            r.Durum,
            r.BolumSayisi,
            r.HazirBolumSayisi,
            r.ToplamSureSn,
            r.TamamlanmaYuzdesi,
            r.KapakVar,
            r.OlusturmaTarihi,
            r.SonDinlenmeTarihi);

    private static string DeriveTitleFromFileName(string fileName)
    {
        var raw = Path.GetFileNameWithoutExtension(fileName)
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();

        return string.IsNullOrWhiteSpace(raw) ? "Adsız Kitap" : raw;
    }
}
