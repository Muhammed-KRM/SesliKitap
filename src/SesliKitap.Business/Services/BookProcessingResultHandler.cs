using System.Text.Json;
using Microsoft.Extensions.Logging;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Logs;
using SesliKitap.Business.Events;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;

namespace SesliKitap.Business.Services;

/// <summary>
/// Python Worker'dan gelen kitap işleme sonuçlarını (<see cref="BookProcessedEvent"/>),
/// aşama ilerlemelerini (<see cref="PipelineStageChangedEvent"/>) ve hataları (<see cref="PipelineErrorEvent"/>) işleyen servis.
/// </summary>
public sealed partial class BookProcessingResultHandler
{
    private static readonly JsonSerializerOptions CamelCaseJson = new(JsonSerializerDefaults.Web);

    private readonly IBookRepository _bookRepository;
    private readonly IChapterRepository _chapterRepository;
    private readonly IFileStorageService _storage;
    private readonly ICacheService _cache;
    private readonly INotificationService _notifications;
    private readonly ILogService _logService;
    private readonly ILogger<BookProcessingResultHandler> _logger;

    /// <summary>
    /// <see cref="BookProcessingResultHandler"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public BookProcessingResultHandler(
        IBookRepository bookRepository,
        IChapterRepository chapterRepository,
        IFileStorageService storage,
        ICacheService cache,
        INotificationService notifications,
        ILogService logService,
        ILogger<BookProcessingResultHandler> logger)
    {
        _bookRepository = bookRepository;
        _chapterRepository = chapterRepository;
        _storage = storage;
        _cache = cache;
        _notifications = notifications;
        _logService = logService;
        _logger = logger;
    }

    /// <summary>
    /// Worker tarafından tamamlanan kitap metin çıkarma ve bölümleme sonucunu veritabanına kaydeder.
    /// </summary>
    public async Task HandleBookProcessedAsync(BookProcessedEvent e, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(e);

        var book = await _bookRepository.GetByIdAsync(e.BookId, ct);
        if (book is null)
        {
            LogBookNotFound(_logger, e.BookId);
            return;
        }

        // İdempotency: Kitap zaten işlenmiş ve bölümleri mevcutsa çift mesajı yoksay
        if (book.Durum is KitapDurumu.OnizlemeBekliyor or KitapDurumu.Seslendiriliyor or KitapDurumu.KismenHazir or KitapDurumu.Hazir)
        {
            var hasChapters = await _chapterRepository.AnyAsync(c => c.BookId == e.BookId, ct);
            if (hasChapters)
            {
                LogDuplicateProcessedEvent(_logger, e.BookId, book.Durum);
                return;
            }
        }

        if (e.OcrGerekli)
        {
            book.Durum = KitapDurumu.OcrGerekli;
            book.SayfaSayisi = e.Meta?.SayfaSayisi ?? book.SayfaSayisi;
            book.KapakYolu = !string.IsNullOrWhiteSpace(e.KapakYolu) ? e.KapakYolu : book.KapakYolu;
            book.Metadata = e.Istatistik is not null ? JsonSerializer.Serialize(e.Istatistik, CamelCaseJson) : null;
            book.HataMesaji = "Bu PDF taranmış görüntülerden oluşuyor (metin katmanı bulunamadı). OCR işlemi gerekiyor.";

            _bookRepository.Update(book);
            await _bookRepository.SaveChangesAsync(ct);

            await _cache.RemoveAsync(CacheKeys.Book(e.BookId), ct);
            await _cache.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);
            await _notifications.BookProcessedAsync(e.BookId, KitapDurumu.OcrGerekli, 0, true, ct);
            return;
        }

        var chaptersKey = !string.IsNullOrWhiteSpace(e.BolumlerYolu)
            ? e.BolumlerYolu
            : StorageKeys.BookChaptersJson(e.BookId);

        var payload = await _storage.ReadJsonAsync<ProcessedChaptersPayload>(chaptersKey, ct);
        if (payload is null || payload.Chapters is null || payload.Chapters.Count == 0)
        {
            throw new PipelineException($"Worker tarafından üretilen bölüm verisi ({chaptersKey}) okunamadı veya boş.");
        }

        var entities = new List<BookChapter>(payload.Chapters.Count);
        foreach (var item in payload.Chapters.OrderBy(c => c.SiraNo))
        {
            var (ruleIncluded, ruleReason) = ChapterTitleRules.Evaluate(item.Baslik);
            var dahilMi = item.DahilMi && ruleIncluded;
            var haricNedeni = !dahilMi
                ? (item.OtomatikHaricNedeni ?? ruleReason)
                : null;

            entities.Add(new BookChapter
            {
                Id = Guid.CreateVersion7(),
                BookId = e.BookId,
                SiraNo = item.SiraNo,
                Baslik = string.IsNullOrWhiteSpace(item.Baslik) ? $"Bölüm {item.SiraNo}" : item.Baslik.Trim(),
                TemizMetin = item.TemizMetin ?? string.Empty,
                KelimeSayisi = item.KelimeSayisi,
                KarakterSayisi = item.KarakterSayisi,
                DahilMi = dahilMi,
                OtomatikHaricNedeni = haricNedeni,
                MetinSurumu = 1,
                SesDurumu = BolumSesDurumu.Yok
            });
        }

        await _bookRepository.ExecuteInTransactionAsync(async token =>
        {
            await _chapterRepository.DeleteByBookIdAsync(e.BookId, token);
            await _chapterRepository.AddRangeAsync(entities, token);

            // Kullanıcının verdiği başlık/yazar ezilmez; yalnızca dosya adından türetilmiş veya boşsa meta veriden doldurulur
            if (e.Meta is not null)
            {
                var fileDerivedTitle = !string.IsNullOrWhiteSpace(book.OrijinalDosyaAdi)
                    ? Path.GetFileNameWithoutExtension(book.OrijinalDosyaAdi).Replace('_', ' ').Replace('-', ' ').Trim()
                    : null;

                if (!string.IsNullOrWhiteSpace(e.Meta.Baslik) &&
                    (string.IsNullOrWhiteSpace(book.Baslik) ||
                     book.Baslik == "Adsız Kitap" ||
                     string.Equals(book.Baslik, fileDerivedTitle, StringComparison.OrdinalIgnoreCase)))
                {
                    book.Baslik = e.Meta.Baslik.Trim();
                }

                if (string.IsNullOrWhiteSpace(book.Yazar) && !string.IsNullOrWhiteSpace(e.Meta.Yazar))
                {
                    book.Yazar = e.Meta.Yazar.Trim();
                }

                if (!string.IsNullOrWhiteSpace(e.Meta.Dil))
                {
                    book.Dil = e.Meta.Dil.Trim();
                }

                book.YayinYili ??= e.Meta.YayinYili;
                book.SayfaSayisi = e.Meta.SayfaSayisi ?? book.SayfaSayisi;
            }

            if (!string.IsNullOrWhiteSpace(e.KapakYolu))
            {
                book.KapakYolu = e.KapakYolu;
            }

            book.KelimeSayisi = entities.Where(c => c.DahilMi).Sum(c => c.KelimeSayisi);
            book.Metadata = e.Istatistik is not null
                ? JsonSerializer.Serialize(e.Istatistik, CamelCaseJson)
                : null;
            book.Durum = KitapDurumu.OnizlemeBekliyor;
            book.HataMesaji = null;

            _bookRepository.Update(book);
            await _bookRepository.SaveChangesAsync(token);
        }, ct);

        await _cache.RemoveAsync(CacheKeys.Book(e.BookId), ct);
        await _cache.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);

        await _notifications.BookProcessedAsync(e.BookId, KitapDurumu.OnizlemeBekliyor, entities.Count, false, ct);
        await _notifications.BookStatusChangedAsync(e.BookId, KitapDurumu.OnizlemeBekliyor, ct);

        await _logService.LogPipelineEventAsync(new PipelineLogRequest
        {
            BookId = e.BookId,
            Asama = nameof(PipelineAsamasi.Tamamlandi),
            Durum = "Tamamlandi",
            Yuzde = 100,
            Mesaj = $"Kitap işleme tamamlandı ({entities.Count} bölüm, {book.KelimeSayisi} kelime).",
            CiktiMetadata = book.Metadata,
            Kaynak = "Api",
            TraceId = e.TraceId
        }, ct);
    }

    /// <summary>
    /// Worker'dan gelen aşama ilerleme olayını işler, SignalR ve PipelineLog'a iletir.
    /// </summary>
    public async Task HandleStageChangedAsync(PipelineStageChangedEvent e, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.BookId.HasValue && !e.JobId.HasValue)
        {
            await _notifications.BookProcessingProgressAsync(e.BookId.Value, e.Asama, e.Yuzde, e.Mesaj, ct);
        }

        await _logService.LogPipelineEventAsync(new PipelineLogRequest
        {
            BookId = e.BookId,
            JobId = e.JobId,
            ChapterId = e.ChapterId,
            Asama = e.Asama,
            Yuzde = e.Yuzde,
            Durum = e.Yuzde >= 100 ? "Tamamlandi" : "Devam",
            Mesaj = e.Mesaj,
            CiktiMetadata = e.Detay is not null ? JsonSerializer.Serialize(e.Detay, CamelCaseJson) : null,
            Kaynak = "Worker",
            TraceId = e.TraceId
        }, ct);
    }

    /// <summary>
    /// Worker'dan gelen pipeline hata olayını işler, kitap durumunu günceller ve SignalR bildirimi gönderir.
    /// </summary>
    public async Task HandleErrorAsync(PipelineErrorEvent e, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.BookId.HasValue && !e.JobId.HasValue)
        {
            await _bookRepository.UpdateStatusAsync(e.BookId.Value, KitapDurumu.Hata, e.HataMesaji, ct);
            await _cache.RemoveAsync(CacheKeys.Book(e.BookId.Value), ct);
            await _cache.RemoveByPrefixAsync(CacheKeys.BookListPrefix, ct);
            await _notifications.BookStatusChangedAsync(e.BookId.Value, KitapDurumu.Hata, ct);
        }

        await _notifications.PipelineErrorAsync(
            e.BookId,
            e.JobId,
            e.VoiceProfileId,
            e.Asama,
            e.HataMesaji,
            e.Kurtarilabilir,
            ct);

        await _logService.LogPipelineEventAsync(new PipelineLogRequest
        {
            BookId = e.BookId,
            JobId = e.JobId,
            ChapterId = e.ChapterId,
            Asama = e.Asama,
            Durum = "Hata",
            Mesaj = e.HataMesaji,
            HataMesaji = $"{e.HataKodu}: {e.HataMesaji}",
            Kaynak = "Worker",
            TraceId = e.TraceId
        }, ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "BookProcessedEvent alındı ancak kitap bulunamadı veya silinmiş: {BookId}")]
    private static partial void LogBookNotFound(ILogger logger, Guid bookId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Kitap zaten işlenmiş ({Durum}), çift BookProcessedEvent yoksayıldı: {BookId}")]
    private static partial void LogDuplicateProcessedEvent(ILogger logger, Guid bookId, KitapDurumu durum);
}
