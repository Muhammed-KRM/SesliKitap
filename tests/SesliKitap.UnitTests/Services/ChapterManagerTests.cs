using FluentAssertions;
using Moq;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Chapter;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Services;
using SesliKitap.Business.Validators;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.Services;

public sealed class ChapterManagerTests
{
    private readonly InMemoryDbContextFactory _dbFactory = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<ILogService> _logMock = new();

    private ChapterManager CreateSut()
    {
        var ctx = _dbFactory.CreateDbContext();
        return new ChapterManager(
            new BookRepository(ctx),
            new ChapterRepository(ctx),
            _cacheMock.Object,
            _logMock.Object,
            new ChapterUpdateRequestValidator(),
            new ChapterMergeRequestValidator(),
            new ChapterSplitRequestValidator(),
            new ChapterOrderRequestValidator(),
            new ChapterInclusionRequestValidator());
    }

    private async Task<(Guid BookId, List<BookChapter> Chapters)> SeedBookWithChaptersAsync()
    {
        var bookId = Guid.CreateVersion7();
        var chapters = new List<BookChapter>
        {
            new()
            {
                Id = Guid.CreateVersion7(),
                BookId = bookId,
                SiraNo = 1,
                Baslik = "Bölüm 1",
                TemizMetin = "Birinci paragraf metni burada yer alıyor.\n\nİkinci paragraf metni burada devam ediyor.",
                KelimeSayisi = 12,
                KarakterSayisi = 84,
                DahilMi = true,
                MetinSurumu = 1,
                SesDurumu = BolumSesDurumu.Hazir,
                OpusYolu = $"books/{bookId:D}/audio/ch1/v1/chapter.ogg"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                BookId = bookId,
                SiraNo = 2,
                Baslik = "Bölüm 2",
                TemizMetin = "İkinci bölümün ilk cümlesi.",
                KelimeSayisi = 4,
                KarakterSayisi = 27,
                DahilMi = true,
                MetinSurumu = 1,
                SesDurumu = BolumSesDurumu.Yok
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                BookId = bookId,
                SiraNo = 3,
                Baslik = "Bölüm 3",
                TemizMetin = "Üçüncü bölümün metni.",
                KelimeSayisi = 3,
                KarakterSayisi = 21,
                DahilMi = true,
                MetinSurumu = 1,
                SesDurumu = BolumSesDurumu.Yok
            }
        };

        await using var ctx = await _dbFactory.CreateDbContextAsync();
        ctx.Books.Add(new Book
        {
            Id = bookId,
            Baslik = "Test Kitabı",
            Durum = KitapDurumu.OnizlemeBekliyor,
            DosyaFormati = DosyaFormati.Pdf,
            KaynakTipi = KaynakTipi.Yukleme,
            DosyaBoyutu = 5000,
            KelimeSayisi = 19
        });
        ctx.BookChapters.AddRange(chapters);
        await ctx.SaveChangesAsync();

        return (bookId, chapters);
    }

    [Fact]
    public async Task UpdateAsync_WhenTextChanges_IncrementsVersionAndMarksAudioGuncelDegil()
    {
        var (_, chapters) = await SeedBookWithChaptersAsync();
        var ch1 = chapters[0];

        var sut = CreateSut();
        var updated = await sut.UpdateAsync(
            ch1.Id,
            new ChapterUpdateRequest(
                "Bölüm 1 Güncel",
                "Tamamen yeni düzenlenmiş metin içeriği.",
                true,
                1));

        updated.MetinSurumu.Should().Be(2);
        updated.SesDurumu.Should().Be(BolumSesDurumu.GuncelDegil);
        updated.KelimeSayisi.Should().Be(5);

        // Eski sürümle tekrar güncelleme 409 ConflictException fırlatmalı
        var staleAct = async () => await CreateSut().UpdateAsync(
            ch1.Id,
            new ChapterUpdateRequest(
                "Eski Sürüm",
                "Başka metin",
                true,
                1));

        await staleAct.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task MergeAsync_NonConsecutiveChapters_ThrowsBusinessException()
    {
        var (bookId, chapters) = await SeedBookWithChaptersAsync();
        var sut = CreateSut();

        var act = async () => await sut.MergeAsync(
            bookId,
            new ChapterMergeRequest([chapters[0].Id, chapters[2].Id])); // SiraNo 1 ve 3 (ardışık değil)

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*ardışık*");
    }

    [Fact]
    public async Task MergeAsync_ConsecutiveChapters_CombinesTextAndShiftsRemainingOrder()
    {
        var (bookId, chapters) = await SeedBookWithChaptersAsync();
        var sut = CreateSut();

        var merged = await sut.MergeAsync(
            bookId,
            new ChapterMergeRequest([chapters[0].Id, chapters[1].Id]));

        merged.SiraNo.Should().Be(1);
        merged.TemizMetin.Should().Contain("İkinci bölümün ilk cümlesi.");

        var summaries = await CreateSut().GetSummariesByBookAsync(bookId);
        summaries.Should().HaveCount(2);
        summaries[0].SiraNo.Should().Be(1);
        summaries[1].SiraNo.Should().Be(2);
        summaries[1].Baslik.Should().Be("Bölüm 3");
    }

    [Fact]
    public async Task SplitAsync_RoundsToNearestParagraphBoundaryAndShiftsOrder()
    {
        var (bookId, chapters) = await SeedBookWithChaptersAsync();
        var ch1 = chapters[0];
        var sut = CreateSut();

        // "\n\n" indeksi 41 civarında; 38 verdiğimizde paragraf sınırından bölmeli
        var summaries = await sut.SplitAsync(
            ch1.Id,
            new ChapterSplitRequest(38, "Bölüm 1B"));

        summaries.Should().HaveCount(4);
        summaries.Select(s => s.SiraNo).Should().Equal(1, 2, 3, 4);
        summaries[1].Baslik.Should().Be("Bölüm 1B");

        var part1 = await CreateSut().GetByIdAsync(summaries[0].Id);
        var part2 = await CreateSut().GetByIdAsync(summaries[1].Id);
        part1.TemizMetin.Should().Be("Birinci paragraf metni burada yer alıyor.");
        part2.TemizMetin.Should().Be("İkinci paragraf metni burada devam ediyor.");
    }

    [Fact]
    public async Task StructuralChange_WhenActiveSynthesisJobExists_ThrowsConflictException()
    {
        var (bookId, chapters) = await SeedBookWithChaptersAsync();
        _cacheMock
            .Setup(c => c.GetStringAsync(CacheKeys.ActiveJob(bookId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.CreateVersion7().ToString());

        var sut = CreateSut();
        var act = async () => await sut.MergeAsync(
            bookId,
            new ChapterMergeRequest([chapters[0].Id, chapters[1].Id]));

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Seslendirme devam ederken*");
    }
}
