using System.Globalization;
using System.IO.Compression;
using System.Text;
using SesliKitap.Business.Constants;
using SesliKitap.Business.Events;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Helpers;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;

namespace SesliKitap.Business.Services;

/// <summary>
/// Kitabın hazır bölümlerini, kapak görselini ve M3U çalma listesini ZIP veya M4B olarak dışa aktaran servis (Bölüm 4.15.4 ve ADR-004).
/// </summary>
public sealed class ExportManager : IExportService
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private readonly IBookRepository _bookRepository;
    private readonly IChapterRepository _chapterRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IEventPublisher? _eventPublisher;

    /// <summary>
    /// <see cref="ExportManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ExportManager(
        IBookRepository bookRepository,
        IChapterRepository chapterRepository,
        IFileStorageService fileStorage,
        IEventPublisher? eventPublisher = null)
    {
        _bookRepository = bookRepository;
        _chapterRepository = chapterRepository;
        _fileStorage = fileStorage;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc />
    public async Task<string> GetZipFileNameAsync(Guid bookId, string format, CancellationToken ct = default)
    {
        var normalizedFormat = NormalizeFormat(format);
        var book = await _bookRepository.GetByIdAsync(bookId, ct)
                   ?? throw new NotFoundException("Kitap bulunamadı.");

        var safeTitle = SanitizeReadableFileName(book.Baslik, "SesliKitap");
        return $"{safeTitle} ({normalizedFormat.ToUpperInvariant()}).zip";
    }

    /// <inheritdoc />
    public async Task<(bool IsReady, string StorageKey, string FileName, long Size)> GetOrRequestM4bAsync(
        Guid bookId,
        string traceId,
        CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(bookId, ct)
                   ?? throw new NotFoundException("Kitap bulunamadı.");

        var chapters = await _chapterRepository.GetByBookIdOrderedAsync(bookId, asNoTracking: true, ct);
        var readyChapters = chapters
            .Where(c => c.DahilMi &&
                        (c.SesDurumu == BolumSesDurumu.Hazir || c.SesDurumu == BolumSesDurumu.GuncelDegil) &&
                        (!string.IsNullOrWhiteSpace(c.OpusYolu) || !string.IsNullOrWhiteSpace(c.Mp3Yolu)))
            .OrderBy(c => c.SiraNo)
            .ToList();

        if (readyChapters.Count == 0)
        {
            throw new BusinessException(
                "Bu kitapta M4B olarak dışa aktarılabilecek hazır sesli bölüm bulunmuyor.",
                ErrorCodes.ValidationFailed);
        }

        var slug = TurkishTextHelper.Slugify(book.Baslik);
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "seslikitap";
        }

        var m4bKey = StorageKeys.BookExportM4b(bookId, slug);
        var safeFileName = $"{SanitizeReadableFileName(book.Baslik, "SesliKitap")}.m4b";

        var info = await _fileStorage.GetInfoAsync(m4bKey, ct);
        if (info is not null && info.Size > 0)
        {
            return (true, m4bKey, safeFileName, info.Size);
        }

        if (_eventPublisher is not null)
        {
            var exportEvent = new BookExportRequestedEvent
            {
                TraceId = traceId,
                BookId = book.Id,
                Baslik = book.Baslik,
                Yazar = book.Yazar,
                KapakYolu = book.KapakYolu,
                CiktiYolu = m4bKey,
                Bolumler = readyChapters
                    .Select(c => new BookExportChapterItem(
                        c.SiraNo,
                        c.Baslik,
                        !string.IsNullOrWhiteSpace(c.OpusYolu) ? c.OpusYolu! : c.Mp3Yolu!,
                        c.SureSn ?? 0))
                    .ToList()
            };

            await _eventPublisher.PublishAsync(exportEvent, ct);
        }

        return (false, m4bKey, safeFileName, 0);
    }

    /// <inheritdoc />
    public async Task WriteM4bAsync(string storageKey, Stream output, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        await _fileStorage.CopyToAsync(storageKey, output, offset: 0, length: null, ct);
        await output.FlushAsync(ct);
    }

    /// <inheritdoc />
    public async Task<string> WriteZipAsync(
        Guid bookId,
        string format,
        Stream output,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var normalizedFormat = NormalizeFormat(format);
        var extension = normalizedFormat == "opus" ? "opus" : "mp3";

        var book = await _bookRepository.GetByIdAsync(bookId, ct)
                   ?? throw new NotFoundException("Kitap bulunamadı.");

        var chapters = await _chapterRepository.GetByBookIdOrderedAsync(bookId, asNoTracking: true, ct);
        var readyChapters = chapters
            .Where(c => c.DahilMi && (c.SesDurumu == BolumSesDurumu.Hazir || c.SesDurumu == BolumSesDurumu.GuncelDegil))
            .OrderBy(c => c.SiraNo)
            .ToList();

        if (readyChapters.Count == 0)
        {
            throw new BusinessException(
                "Bu kitapta dışa aktarılabilecek hazır sesli bölüm bulunmuyor.",
                ErrorCodes.ValidationFailed);
        }

        var safeBookTitle = SanitizeReadableFileName(book.Baslik, "SesliKitap");
        var zipFileName = $"{safeBookTitle} ({normalizedFormat.ToUpperInvariant()}).zip";

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"sk-export-{Guid.NewGuid():N}.zip");
        await using var tempStream = new FileStream(
            tempFilePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 65536,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

        var m3uBuilder = new StringBuilder();
        m3uBuilder.AppendLine("#EXTM3U");
        m3uBuilder.AppendLine(CultureInfo.InvariantCulture, $"#PLAYLIST:{book.Baslik}");

        using (var archive = new ZipArchive(tempStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var chapter in readyChapters)
            {
                ct.ThrowIfCancellationRequested();

                var storageKey = normalizedFormat == "opus" ? chapter.OpusYolu : chapter.Mp3Yolu;
                if (string.IsNullOrWhiteSpace(storageKey))
                {
                    continue;
                }

                var safeChapterTitle = SanitizeReadableFileName(chapter.Baslik, $"Bölüm {chapter.SiraNo}");
                var entryFileName = $"{chapter.SiraNo:D2} - {safeChapterTitle}.{extension}";

                var entry = archive.CreateEntry(entryFileName, CompressionLevel.NoCompression);
                await using (var entryStream = entry.Open())
                {
                    await _fileStorage.CopyToAsync(storageKey, entryStream, offset: 0, length: null, ct);
                }

                var durationSec = (int)Math.Round(chapter.SureSn ?? 0);
                var displayArtist = string.IsNullOrWhiteSpace(book.Yazar) ? book.Baslik : $"{book.Yazar} - {book.Baslik}";
                m3uBuilder.AppendLine(CultureInfo.InvariantCulture, $"#EXTINF:{durationSec},{displayArtist} - {chapter.Baslik}");
                m3uBuilder.AppendLine(entryFileName);
            }

            if (!string.IsNullOrWhiteSpace(book.KapakYolu) &&
                await _fileStorage.GetInfoAsync(book.KapakYolu, ct) is not null)
            {
                var coverEntry = archive.CreateEntry("cover.jpg", CompressionLevel.NoCompression);
                await using var coverEntryStream = coverEntry.Open();
                await _fileStorage.CopyToAsync(book.KapakYolu, coverEntryStream, offset: 0, length: null, ct);
            }

            var playlistEntry = archive.CreateEntry("playlist.m3u", CompressionLevel.Fastest);
            await using (var playlistStream = playlistEntry.Open())
            {
                var utf8Bytes = Encoding.UTF8.GetBytes(m3uBuilder.ToString());
                await playlistStream.WriteAsync(utf8Bytes, ct);
            }
        }

        await tempStream.FlushAsync(ct);
        tempStream.Position = 0;
        await tempStream.CopyToAsync(output, 65536, ct);
        await output.FlushAsync(ct);

        return zipFileName;
    }

    private static string NormalizeFormat(string? format)
    {
        var normalized = (format ?? "mp3").Trim().ToLowerInvariant();
        if (normalized is not ("mp3" or "opus"))
        {
            throw new BusinessException(
                "Desteklenmeyen dışa aktarım formatı. Yalnızca 'mp3' veya 'opus' seçilebilir.",
                ErrorCodes.ValidationFailed);
        }

        return normalized;
    }

    /// <summary>
    /// Türkçe karakterleri koruyarak dosya sisteminde geçersiz olan karakterleri temizler.
    /// </summary>
    public static string SanitizeReadableFileName(string? input, string fallback)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return fallback;
        }

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input.Trim())
        {
            if (Array.IndexOf(InvalidFileNameChars, ch) >= 0 || ch is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|')
            {
                sb.Append(' ');
            }
            else if (char.IsControl(ch))
            {
                continue;
            }
            else
            {
                sb.Append(ch);
            }
        }

        var collapsed = string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim('.');
        if (string.IsNullOrWhiteSpace(collapsed))
        {
            return fallback;
        }

        return collapsed.Length > 120 ? collapsed[..120].Trim() : collapsed;
    }
}
