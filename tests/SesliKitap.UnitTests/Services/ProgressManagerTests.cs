using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Moq;
using SesliKitap.Business.DTOs.Progress;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Services;
using SesliKitap.Business.Validators;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.Services;

public sealed class ProgressManagerTests
{
    private readonly FakeClock _clock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<INotificationService> _notificationMock = new();

    [Fact]
    public async Task UpdateAsync_CalculatesCompletionPercentAndSavesProgress()
    {
        await using var db = new InMemoryDbContextFactory().CreateDbContext();
        var bookRepo = new BookRepository(db);
        var chapterRepo = new ChapterRepository(db);
        var progressRepo = new ProgressRepository(db);

        var book = new Book
        {
            Id = Guid.CreateVersion7(),
            Baslik = "Konum Test Kitabı",
            Durum = KitapDurumu.Hazir,
            ToplamSureSn = 300
        };
        var ch1 = new BookChapter
        {
            Id = Guid.CreateVersion7(),
            BookId = book.Id,
            SiraNo = 1,
            Baslik = "Bölüm 1",
            TemizMetin = "Metin 1",
            KelimeSayisi = 200,
            DahilMi = true,
            SesDurumu = BolumSesDurumu.Hazir,
            SureSn = 100
        };
        var ch2 = new BookChapter
        {
            Id = Guid.CreateVersion7(),
            BookId = book.Id,
            SiraNo = 2,
            Baslik = "Bölüm 2",
            TemizMetin = "Metin 2",
            KelimeSayisi = 400,
            DahilMi = true,
            SesDurumu = BolumSesDurumu.Hazir,
            SureSn = 200
        };

        await db.Books.AddAsync(book);
        await db.BookChapters.AddRangeAsync(ch1, ch2);
        await db.SaveChangesAsync();

        var sut = new ProgressManager(
            bookRepo,
            chapterRepo,
            progressRepo,
            _cacheMock.Object,
            _notificationMock.Object,
            _clock,
            new ProgressUpdateRequestValidator());

        var req = new ProgressUpdateRequest
        {
            ChapterId = ch2.Id,
            PozisyonSn = 50,
            OynatmaHizi = 1.25,
            CihazKimligi = "dev-pc-1",
            CihazAdi = "Windows · Chrome",
            IstemciZamani = _clock.UtcNow.UtcDateTime.AddSeconds(-2)
        };

        var dto = await sut.UpdateAsync(book.Id, req, "conn-1");

        dto.ChapterId.Should().Be(ch2.Id);
        dto.BolumSiraNo.Should().Be(2);
        dto.PozisyonSn.Should().Be(50);
        dto.OynatmaHizi.Should().Be(1.25);
        // (100 + 50) / 300 = %50
        dto.TamamlanmaYuzdesi.Should().Be(50.0);
    }

    [Fact]
    public async Task UpdateAsync_OlderClientTimestamp_DoesNotOverwriteNewerPosition()
    {
        await using var db = new InMemoryDbContextFactory().CreateDbContext();
        var bookRepo = new BookRepository(db);
        var chapterRepo = new ChapterRepository(db);
        var progressRepo = new ProgressRepository(db);

        var book = new Book
        {
            Id = Guid.CreateVersion7(),
            Baslik = "Senkron Kitabı",
            Durum = KitapDurumu.Hazir,
            ToplamSureSn = 200
        };
        var ch1 = new BookChapter
        {
            Id = Guid.CreateVersion7(),
            BookId = book.Id,
            SiraNo = 1,
            Baslik = "Bölüm 1",
            TemizMetin = "Metin",
            KelimeSayisi = 400,
            DahilMi = true,
            SesDurumu = BolumSesDurumu.Hazir,
            SureSn = 200
        };

        await db.Books.AddAsync(book);
        await db.BookChapters.AddAsync(ch1);
        await db.SaveChangesAsync();

        var sut = new ProgressManager(
            bookRepo,
            chapterRepo,
            progressRepo,
            _cacheMock.Object,
            _notificationMock.Object,
            _clock,
            new ProgressUpdateRequestValidator());

        var newerTime = _clock.UtcNow.UtcDateTime;
        var olderTime = _clock.UtcNow.UtcDateTime.AddMinutes(-5);

        await sut.UpdateAsync(book.Id, new ProgressUpdateRequest
        {
            ChapterId = ch1.Id,
            PozisyonSn = 120,
            OynatmaHizi = 1.5,
            CihazKimligi = "pc",
            CihazAdi = "PC",
            IstemciZamani = newerTime
        });

        var resultAfterStale = await sut.UpdateAsync(book.Id, new ProgressUpdateRequest
        {
            ChapterId = ch1.Id,
            PozisyonSn = 35,
            OynatmaHizi = 1.0,
            CihazKimligi = "phone",
            CihazAdi = "Telefon",
            IstemciZamani = olderTime
        });

        resultAfterStale.PozisyonSn.Should().Be(120);
        resultAfterStale.OynatmaHizi.Should().Be(1.5);
        resultAfterStale.CihazKimligi.Should().Be("pc");
    }

    [Fact]
    public async Task UpdateAsync_InvalidPositionOrRate_ThrowsBusinessException()
    {
        await using var db = new InMemoryDbContextFactory().CreateDbContext();
        var bookRepo = new BookRepository(db);
        var chapterRepo = new ChapterRepository(db);
        var progressRepo = new ProgressRepository(db);

        var book = new Book
        {
            Id = Guid.CreateVersion7(),
            Baslik = "Sınır Testi",
            Durum = KitapDurumu.Hazir
        };
        var ch1 = new BookChapter
        {
            Id = Guid.CreateVersion7(),
            BookId = book.Id,
            SiraNo = 1,
            Baslik = "Bölüm 1",
            TemizMetin = "Metin",
            KelimeSayisi = 100,
            DahilMi = true,
            SesDurumu = BolumSesDurumu.Hazir,
            SureSn = 60
        };

        await db.Books.AddAsync(book);
        await db.BookChapters.AddAsync(ch1);
        await db.SaveChangesAsync();

        var sut = new ProgressManager(
            bookRepo,
            chapterRepo,
            progressRepo,
            _cacheMock.Object,
            _notificationMock.Object,
            _clock,
            new ProgressUpdateRequestValidator());

        // Bölüm süresi 60 sn iken 80 sn gönderilmesi -> hata
        var actExceed = () => sut.UpdateAsync(book.Id, new ProgressUpdateRequest
        {
            ChapterId = ch1.Id,
            PozisyonSn = 80,
            OynatmaHizi = 1.0,
            CihazKimligi = "pc",
            IstemciZamani = _clock.UtcNow.UtcDateTime
        });
        await actExceed.Should().ThrowAsync<BusinessException>();

        // Oynatma hızı 4.0 -> hata
        var actRate = () => sut.UpdateAsync(book.Id, new ProgressUpdateRequest
        {
            ChapterId = ch1.Id,
            PozisyonSn = 20,
            OynatmaHizi = 4.0,
            CihazKimligi = "pc",
            IstemciZamani = _clock.UtcNow.UtcDateTime
        });
        await actRate.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task ExportManager_WriteZipAsync_CreatesValidZipWithAudioCoverAndM3u()
    {
        await using var db = new InMemoryDbContextFactory().CreateDbContext();
        var bookRepo = new BookRepository(db);
        var chapterRepo = new ChapterRepository(db);
        var storageMock = new Mock<IFileStorageService>();

        var book = new Book
        {
            Id = Guid.CreateVersion7(),
            Baslik = "Çalıkuşu: Özel Baskı",
            Yazar = "Reşat Nuri Güntekin",
            KapakYolu = "books/1/cover.jpg",
            Durum = KitapDurumu.Hazir
        };
        var ch1 = new BookChapter
        {
            Id = Guid.CreateVersion7(),
            BookId = book.Id,
            SiraNo = 1,
            Baslik = "Birinci Bölüm",
            TemizMetin = "Test",
            DahilMi = true,
            SesDurumu = BolumSesDurumu.Hazir,
            Mp3Yolu = "books/1/audio/ch1/v1/chapter.mp3",
            OpusYolu = "books/1/audio/ch1/v1/chapter.ogg",
            SureSn = 125
        };

        await db.Books.AddAsync(book);
        await db.BookChapters.AddAsync(ch1);
        await db.SaveChangesAsync();

        storageMock.Setup(s => s.GetInfoAsync("books/1/cover.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageObjectInfo("books/1/cover.jpg", 3, "\"etag\"", "image/jpeg"));
        storageMock.Setup(s => s.CopyToAsync("books/1/cover.jpg", It.IsAny<Stream>(), 0, null, It.IsAny<CancellationToken>()))
            .Callback<string, Stream, long, long?, CancellationToken>((_, dest, _, _, _) =>
            {
                dest.Write(new byte[] { 0xFF, 0xD8, 0xFF });
            })
            .Returns(Task.CompletedTask);
        storageMock.Setup(s => s.CopyToAsync("books/1/audio/ch1/v1/chapter.mp3", It.IsAny<Stream>(), 0, null, It.IsAny<CancellationToken>()))
            .Callback<string, Stream, long, long?, CancellationToken>((_, dest, _, _, _) =>
            {
                dest.Write(Encoding.ASCII.GetBytes("ID3-DUMMY-MP3-BYTES"));
            })
            .Returns(Task.CompletedTask);

        var exportManager = new ExportManager(bookRepo, chapterRepo, storageMock.Object);
        using var zipOut = new MemoryStream();

        var fileName = await exportManager.WriteZipAsync(book.Id, "mp3", zipOut);

        fileName.Should().Be("Çalıkuşu Özel Baskı (MP3).zip");
        zipOut.Position = 0;

        using var archive = new ZipArchive(zipOut, ZipArchiveMode.Read);
        archive.Entries.Select(e => e.FullName).Should().Contain("01 - Birinci Bölüm.mp3");
        archive.Entries.Select(e => e.FullName).Should().Contain("cover.jpg");
        archive.Entries.Select(e => e.FullName).Should().Contain("playlist.m3u");
    }
}
