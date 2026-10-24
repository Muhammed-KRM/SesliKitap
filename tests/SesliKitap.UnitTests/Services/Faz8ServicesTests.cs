using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using SesliKitap.Business.DTOs.Bookmark;
using SesliKitap.Business.DTOs.Pronunciation;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;
using SesliKitap.Business.Services;
using SesliKitap.Business.Validators;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.Services;

public sealed class Faz8ServicesTests
{
    [Fact]
    public async Task PronunciationManager_CreatePreviewAndConflict_WorksCorrectly()
    {
        await using var db = new InMemoryDbContextFactory().CreateDbContext();
        var cacheMock = new Mock<ICacheService>();
        var manager = new PronunciationManager(
            db,
            cacheMock.Object,
            new PronunciationCreateRequestValidator(),
            new PronunciationUpdateRequestValidator(),
            new PronunciationPreviewRequestValidator());

        var created = await manager.CreateAsync(new PronunciationCreateRequest(
            "Nietzsche",
            "niçe",
            BuyukKucukDuyarli: false,
            AktifMi: true,
            Aciklama: "Filozof"));

        created.Kelime.Should().Be("Nietzsche");
        created.Okunus.Should().Be("niçe");

        // Aynı kelime (büyük/küçük harf farkıyla) eklenirse 409 Conflict fırlatmalı
        var act = () => manager.CreateAsync(new PronunciationCreateRequest("nietzsche", "niçe"));
        await act.Should().ThrowAsync<ConflictException>();

        // Önizleme: tam kelime ve kesme işaretli ekleri değiştirmeli
        var preview = await manager.PreviewAsync(new PronunciationPreviewRequest(
            "Friedrich Nietzsche ve Nietzsche'nin eserleri incelendi."));

        preview.DegisenKelimeSayisi.Should().Be(2);
        preview.OnizlemeMetni.Should().Be("Friedrich niçe ve niçenin eserleri incelendi.");
    }

    [Fact]
    public async Task BookmarkManager_CreateListAndDelete_WorksCorrectly()
    {
        await using var db = new InMemoryDbContextFactory().CreateDbContext();
        var bookId = Guid.CreateVersion7();
        var ch1Id = Guid.CreateVersion7();
        var ch2Id = Guid.CreateVersion7();

        db.Books.Add(new Book
        {
            Id = bookId,
            Baslik = "Tutunamayanlar",
            Dil = "tr",
            Durum = KitapDurumu.Hazir,
            DosyaFormati = DosyaFormati.Epub
        });
        db.BookChapters.AddRange(
            new BookChapter
            {
                Id = ch1Id,
                BookId = bookId,
                SiraNo = 1,
                Baslik = "1. Bölüm",
                TemizMetin = "Metin 1",
                SureSn = 300
            },
            new BookChapter
            {
                Id = ch2Id,
                BookId = bookId,
                SiraNo = 2,
                Baslik = "2. Bölüm",
                TemizMetin = "Metin 2",
                SureSn = 400
            });
        await db.SaveChangesAsync();

        var manager = new BookmarkManager(db);

        var b2 = await manager.CreateAsync(bookId, new BookmarkCreateRequest(ch2Id, 120.5, "Önemli alıntı"));
        var b1 = await manager.CreateAsync(bookId, new BookmarkCreateRequest(ch1Id, 45.0, "Başlangıç notu"));

        var list = await manager.GetByBookAsync(bookId);
        list.Should().HaveCount(2);
        list[0].Id.Should().Be(b1.Id);
        list[0].ChapterSiraNo.Should().Be(1);
        list[1].Id.Should().Be(b2.Id);
        list[1].ChapterSiraNo.Should().Be(2);

        await manager.DeleteAsync(b1.Id);
        var afterDelete = await manager.GetByBookAsync(bookId);
        afterDelete.Should().ContainSingle(x => x.Id == b2.Id);
    }

    [Fact]
    public async Task UsageManager_GetMonthlyUsage_AggregatesGpuCostAndSixMonthHistory()
    {
        await using var db = new InMemoryDbContextFactory().CreateDbContext();
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero) };
        var ttsOpts = Options.Create(new TtsOptions { MonthlyBudgetUsd = 30.0m });

        var bookId = Guid.CreateVersion7();
        var voiceId = Guid.CreateVersion7();

        db.SynthesisJobs.AddRange(
            new SynthesisJob
            {
                Id = Guid.CreateVersion7(),
                BookId = bookId,
                VoiceProfileId = voiceId,
                MotorSirasi = "voxcpm2_modal",
                Durum = SentezIsDurumu.Tamamlandi,
                GpuMs = 3_600_000, // 1 saat GPU
                TahminiMaliyetUsd = 0.92m,
                UretilenSesSn = 18_000, // 5 saat ses
                OnbellektenParca = 50,
                UretilenParca = 200,
                OlusturmaTarihi = new DateTime(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc)
            },
            new SynthesisJob
            {
                Id = Guid.CreateVersion7(),
                BookId = Guid.CreateVersion7(),
                VoiceProfileId = voiceId,
                MotorSirasi = "voxcpm2_modal",
                Durum = SentezIsDurumu.Tamamlandi,
                GpuMs = 1_800_000, // 0.5 saat GPU (Eylül 2026)
                TahminiMaliyetUsd = 0.46m,
                UretilenSesSn = 9_000,
                OnbellektenParca = 10,
                UretilenParca = 100,
                OlusturmaTarihi = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc)
            });
        await db.SaveChangesAsync();

        var manager = new UsageManager(db, clock, ttsOpts);
        var report = await manager.GetMonthlyUsageAsync(2026, 10);

        report.Yil.Should().Be(2026);
        report.Ay.Should().Be(10);
        report.ToplamGpuMs.Should().Be(3_600_000);
        report.ToplamGpuSaat.Should().Be(1.0);
        report.ToplamMaliyetUsd.Should().Be(0.92m);
        report.UretilenSesSaat.Should().Be(5.0);
        report.TamamlananIsSayisi.Should().Be(1);
        report.SeslendirilenKitapSayisi.Should().Be(1);
        report.SonAltiAy.Should().HaveCount(6);
        report.SonAltiAy[^1].ToplamMaliyetUsd.Should().Be(0.92m);
        report.SonAltiAy[^2].ToplamMaliyetUsd.Should().Be(0.46m);
    }
}
