using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SesliKitap.Business.DTOs.Chapter;
using SesliKitap.Business.Events;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Services;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.Services;

public sealed class BookProcessingResultHandlerTests
{
    private readonly InMemoryDbContextFactory _dbFactory = new();
    private readonly Mock<IFileStorageService> _storageMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<INotificationService> _notificationsMock = new();
    private readonly Mock<ILogService> _logMock = new();

    private BookProcessingResultHandler CreateSut()
    {
        var ctx = _dbFactory.CreateDbContext();
        return new BookProcessingResultHandler(
            new BookRepository(ctx),
            new ChapterRepository(ctx),
            _storageMock.Object,
            _cacheMock.Object,
            _notificationsMock.Object,
            _logMock.Object,
            NullLogger<BookProcessingResultHandler>.Instance);
    }

    [Fact]
    public async Task HandleBookProcessedAsync_SavesChaptersPreservesCustomTitleAndMarksOnizlemeBekliyor()
    {
        var bookId = Guid.CreateVersion7();
        await using (var seedCtx = await _dbFactory.CreateDbContextAsync())
        {
            seedCtx.Books.Add(new Book
            {
                Id = bookId,
                Baslik = "Kullanıcının Özel Başlığı",
                OrijinalDosyaAdi = "ham_dosya.pdf",
                Durum = KitapDurumu.Isleniyor,
                DosyaFormati = DosyaFormati.Pdf,
                KaynakTipi = KaynakTipi.Yukleme,
                DosyaBoyutu = 1024
            });
            await seedCtx.SaveChangesAsync();
        }

        var chaptersKey = $"books/{bookId:D}/processed/chapters.json";
        _storageMock
            .Setup(s => s.ReadJsonAsync<ProcessedChaptersPayload>(chaptersKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedChaptersPayload(
                bookId,
                [
                    new ProcessedChapterItem(1, "İçindekiler", "Bölüm 1 ... 5", 3, 13, true, null),
                    new ProcessedChapterItem(2, "Birinci Bölüm", "Ana metin burada başlar.", 4, 24, true, null)
                ]));

        var sut = CreateSut();
        await sut.HandleBookProcessedAsync(new BookProcessedEvent
        {
            BookId = bookId,
            BolumlerYolu = chaptersKey,
            KapakYolu = $"books/{bookId:D}/cover.jpg",
            Meta = new ProcessedBookMeta("Dosyadaki Başlık", "Victor Hugo", "tr", 1862, 412),
            Istatistik = new ProcessedBookStats(10, 12, 2, 35, "pdf_outline"),
            OcrGerekli = false
        });

        await using var verifyCtx = await _dbFactory.CreateDbContextAsync();
        var updatedBook = verifyCtx.Books.Single(b => b.Id == bookId);
        updatedBook.Durum.Should().Be(KitapDurumu.OnizlemeBekliyor);
        // Kullanıcının özel başlığı ezilmemeli, ancak boş olan yazar doldurulmalı
        updatedBook.Baslik.Should().Be("Kullanıcının Özel Başlığı");
        updatedBook.Yazar.Should().Be("Victor Hugo");
        updatedBook.KelimeSayisi.Should().Be(4); // İçindekiler otomatik hariç tutulduğu için yalnız 2. bölüm

        var chapters = verifyCtx.BookChapters.Where(c => c.BookId == bookId).OrderBy(c => c.SiraNo).ToList();
        chapters.Should().HaveCount(2);
        chapters[0].DahilMi.Should().BeFalse(); // Sunucu tarafı ChapterTitleRules kontrolü
        chapters[1].DahilMi.Should().BeTrue();
    }

    [Fact]
    public async Task HandleBookProcessedAsync_DuplicateMessage_IsIdempotent()
    {
        var bookId = Guid.CreateVersion7();
        await using (var seedCtx = await _dbFactory.CreateDbContextAsync())
        {
            seedCtx.Books.Add(new Book
            {
                Id = bookId,
                Baslik = "Kitap",
                Durum = KitapDurumu.OnizlemeBekliyor,
                DosyaFormati = DosyaFormati.Pdf,
                KaynakTipi = KaynakTipi.Yukleme,
                DosyaBoyutu = 1024
            });
            seedCtx.BookChapters.Add(new BookChapter
            {
                Id = Guid.CreateVersion7(),
                BookId = bookId,
                SiraNo = 1,
                Baslik = "Bölüm 1",
                TemizMetin = "Metin",
                KelimeSayisi = 1,
                KarakterSayisi = 5
            });
            await seedCtx.SaveChangesAsync();
        }

        var sut = CreateSut();
        await sut.HandleBookProcessedAsync(new BookProcessedEvent
        {
            BookId = bookId,
            BolumlerYolu = $"books/{bookId:D}/processed/chapters.json",
            OcrGerekli = false
        });

        _storageMock.Verify(
            s => s.ReadJsonAsync<ProcessedChaptersPayload>(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleBookProcessedAsync_WhenOcrRequired_MarksBookStatusAsOcrGerekli()
    {
        var bookId = Guid.CreateVersion7();
        await using (var seedCtx = await _dbFactory.CreateDbContextAsync())
        {
            seedCtx.Books.Add(new Book
            {
                Id = bookId,
                Baslik = "Taranmış Kitap",
                Durum = KitapDurumu.Isleniyor,
                DosyaFormati = DosyaFormati.Pdf,
                KaynakTipi = KaynakTipi.Yukleme,
                DosyaBoyutu = 2048
            });
            await seedCtx.SaveChangesAsync();
        }

        var sut = CreateSut();
        await sut.HandleBookProcessedAsync(new BookProcessedEvent
        {
            BookId = bookId,
            OcrGerekli = true,
            Meta = new ProcessedBookMeta(null, null, "tr", null, 150)
        });

        await using var verifyCtx = await _dbFactory.CreateDbContextAsync();
        var updated = verifyCtx.Books.Single(b => b.Id == bookId);
        updated.Durum.Should().Be(KitapDurumu.OcrGerekli);
        updated.SayfaSayisi.Should().Be(150);
    }
}
