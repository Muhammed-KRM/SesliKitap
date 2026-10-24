using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Logs;
using SesliKitap.Business.DTOs.Synthesis;
using SesliKitap.Business.Events;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;
using SesliKitap.Business.Services;
using SesliKitap.Business.Validators;
using SesliKitap.Data.Context;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.Services;

public sealed class SynthesisManagerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SynthesisJobRepository _jobRepository;
    private readonly BookRepository _bookRepository;
    private readonly ChapterRepository _chapterRepository;
    private readonly VoiceProfileRepository _voiceRepository;
    private readonly Mock<IFileStorageService> _storageMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IEventPublisher> _publisherMock = new();
    private readonly Mock<INotificationService> _notificationsMock = new();
    private readonly Mock<ILogService> _logServiceMock = new();
    private readonly FakeClock _clock = new() { UtcNow = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero) };
    private readonly SynthesisDefaultsOptions _defaultsOptions = new();

    public SynthesisManagerTests()
    {
        _db = new InMemoryDbContextFactory().CreateDbContext();
        _jobRepository = new SynthesisJobRepository(_db);
        _bookRepository = new BookRepository(_db);
        _chapterRepository = new ChapterRepository(_db);
        _voiceRepository = new VoiceProfileRepository(_db);

        _cacheMock
            .Setup(x => x.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _logServiceMock
            .Setup(x => x.LogPipelineStartAsync(It.IsAny<PipelineLogContext>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private SynthesisManager CreateSut(decimal monthlyBudgetUsd = 30.0m)
    {
        var ttsOptions = new TtsOptions
        {
            WordsPerMinute = 140,
            EstimatedRtf = 0.25,
            ColdStartSec = 60,
            IdleOverheadFactor = 1.15,
            GpuPricePerHourUsd = 0.59m,
            MonthlyBudgetUsd = monthlyBudgetUsd,
            EngineOrder = ["voxcpm2_modal", "xtts2_modal"]
        };

        var costEstimator = new CostEstimator(Options.Create(ttsOptions));

        return new SynthesisManager(
            _jobRepository,
            _bookRepository,
            _chapterRepository,
            _voiceRepository,
            _storageMock.Object,
            _cacheMock.Object,
            _publisherMock.Object,
            _notificationsMock.Object,
            costEstimator,
            _logServiceMock.Object,
            _clock,
            new SynthesisStartRequestValidator(),
            Options.Create(ttsOptions),
            Options.Create(_defaultsOptions));
    }

    private async Task<(Book Book, VoiceProfile Voice, List<BookChapter> Chapters)> SeedReadyBookAndVoiceAsync()
    {
        var book = new Book
        {
            Id = Guid.CreateVersion7(),
            Baslik = "Kürk Mantolu Madonna",
            Yazar = "Sabahattin Ali",
            Dil = "tr",
            KaynakTipi = KaynakTipi.Yukleme,
            DosyaFormati = DosyaFormati.Pdf,
            OrijinalDosyaAdi = "kurk.pdf",
            OrijinalDosyaYolu = "books/1/source.pdf",
            DosyaHash = new string('a', 64),
            Durum = KitapDurumu.OnizlemeBekliyor
        };

        var ch1 = new BookChapter
        {
            Id = Guid.CreateVersion7(),
            BookId = book.Id,
            SiraNo = 1,
            Baslik = "Birinci Bölüm",
            TemizMetin = "Raif Efendi sessiz bir adamdı.\n\nBerlin yıllarını kimseye anlatmazdı.",
            KelimeSayisi = 1_400,
            KarakterSayisi = 9_800,
            MetinSurumu = 1,
            DahilMi = true,
            SesDurumu = BolumSesDurumu.Yok
        };

        var ch2 = new BookChapter
        {
            Id = Guid.CreateVersion7(),
            BookId = book.Id,
            SiraNo = 2,
            Baslik = "İkinci Bölüm",
            TemizMetin = "Sergideki tabloya uzun uzun baktı.\n\nMaria Puder ile tanıştı.",
            KelimeSayisi = 1_400,
            KarakterSayisi = 9_500,
            MetinSurumu = 1,
            DahilMi = true,
            SesDurumu = BolumSesDurumu.Yok
        };

        var voice = new VoiceProfile
        {
            Id = Guid.CreateVersion7(),
            Ad = "Anlatıcı Sesi",
            ReferansSesYolu = "voices/1/reference.wav",
            ReferansHash = new string('b', 64),
            ReferansSureSn = 15.2,
            ReferansMetin = "Merhaba dünya.",
            Durum = SesProfiliDurumu.Hazir,
            VarsayilanMi = true
        };

        _db.Books.Add(book);
        _db.BookChapters.AddRange(ch1, ch2);
        _db.VoiceProfiles.Add(voice);
        await _db.SaveChangesAsync();

        return (book, voice, [ch1, ch2]);
    }

    [Fact]
    public async Task EstimateAsync_ValidBookAndVoice_ReturnsAccurateDurationAndBudget()
    {
        var sut = CreateSut();
        var (book, voice, _) = await SeedReadyBookAndVoiceAsync();

        var estimate = await sut.EstimateAsync(book.Id, voice.Id, chapterIds: null, sadeceEksikler: true);

        estimate.BolumSayisi.Should().Be(2);
        estimate.KelimeSayisi.Should().Be(2_800);
        estimate.TahminiSesSn.Should().BeApproximately(1_200.0, 1.0);
        estimate.TahminiMaliyetUsd.Should().BeGreaterThan(0m);
        estimate.ButceAsiliyor.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_BudgetExceededWithoutForce_ThrowsBusinessException()
    {
        var sut = CreateSut(monthlyBudgetUsd: 0.01m);
        var (book, voice, _) = await SeedReadyBookAndVoiceAsync();

        var act = () => sut.StartAsync(
            book.Id,
            new SynthesisStartRequest(voice.Id, null, SadeceEksikler: true, Force: false),
            "trace-budget");

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage($"*{ErrorCodes.SynthesisBudgetExceeded}*");
    }

    [Fact]
    public async Task StartAsync_BudgetExceededWithForce_StartsJobSuccessfully()
    {
        var sut = CreateSut(monthlyBudgetUsd: 0.01m);
        var (book, voice, _) = await SeedReadyBookAndVoiceAsync();

        SynthesisInputPayload? uploadedPayload = null;
        _storageMock
            .Setup(x => x.UploadJsonAsync(It.IsAny<string>(), It.IsAny<SynthesisInputPayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, SynthesisInputPayload, CancellationToken>((_, payload, _) => uploadedPayload = payload)
            .Returns(Task.CompletedTask);

        var jobDto = await sut.StartAsync(
            book.Id,
            new SynthesisStartRequest(voice.Id, null, SadeceEksikler: true, Force: true),
            "trace-force");

        jobDto.Durum.Should().Be(SentezIsDurumu.Kuyrukta);
        jobDto.ToplamBolum.Should().Be(2);
        uploadedPayload.Should().NotBeNull();
        uploadedPayload!.Chapters.Should().HaveCount(2);
        uploadedPayload.VoiceProfile.ReferansHash.Should().Be(voice.ReferansHash);

        _publisherMock.Verify(
            x => x.PublishAsync(It.Is<SynthesisRequestedEvent>(e => e.JobId == jobDto.Id && e.BookId == book.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_RedisLockNotAcquired_ThrowsConflictException409()
    {
        var sut = CreateSut();
        var (book, voice, _) = await SeedReadyBookAndVoiceAsync();
        var activeId = Guid.CreateVersion7();

        _cacheMock
            .Setup(x => x.TryAcquireLockAsync(CacheKeys.ActiveJob(book.Id), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _cacheMock
            .Setup(x => x.GetStringAsync(CacheKeys.ActiveJob(book.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeId.ToString("D"));

        var act = () => sut.StartAsync(
            book.Id,
            new SynthesisStartRequest(voice.Id, null, SadeceEksikler: true, Force: false),
            "trace-lock");

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ExistingId.Should().Be(activeId);
    }

    [Fact]
    public async Task StartAsync_PublishFails_RollsBackBookAndChaptersAndReleasesLock()
    {
        var sut = CreateSut();
        var (book, voice, chapters) = await SeedReadyBookAndVoiceAsync();

        _publisherMock
            .Setup(x => x.PublishAsync(It.IsAny<SynthesisRequestedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("RabbitMQ down"));

        var act = () => sut.StartAsync(
            book.Id,
            new SynthesisStartRequest(voice.Id, null, SadeceEksikler: true, Force: false),
            "trace-fail");

        await act.Should().ThrowAsync<ExternalServiceException>();

        var reloadedBook = await _bookRepository.GetByIdAsync(book.Id);
        reloadedBook!.Durum.Should().Be(KitapDurumu.OnizlemeBekliyor);

        var reloadedCh1 = await _chapterRepository.GetByIdAsync(chapters[0].Id);
        reloadedCh1!.SesDurumu.Should().Be(BolumSesDurumu.Yok);

        _cacheMock.Verify(x => x.RemoveAsync(CacheKeys.ActiveJob(book.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ActiveJob_SetsRedisCancelFlagAndRestoresQueuedChapters()
    {
        var sut = CreateSut();
        var (book, voice, _) = await SeedReadyBookAndVoiceAsync();
        var jobDto = await sut.StartAsync(
            book.Id,
            new SynthesisStartRequest(voice.Id, null, SadeceEksikler: true, Force: false),
            "trace-cancel");

        await sut.CancelAsync(jobDto.Id);

        _cacheMock.Verify(
            x => x.SetStringAsync(CacheKeys.JobCancel(jobDto.Id), "1", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheMock.Verify(
            x => x.RemoveAsync(CacheKeys.ActiveJob(book.Id), It.IsAny<CancellationToken>()),
            Times.Once);

        var detail = await sut.GetByIdAsync(jobDto.Id);
        detail.Is.Durum.Should().Be(SentezIsDurumu.IptalEdildi);
    }

    [Fact]
    public async Task HandleChapterSynthesizedAsync_IdempotentAndMarksGuncelDegilWhenTextEditedMidJob()
    {
        var sut = CreateSut();
        var (book, voice, chapters) = await SeedReadyBookAndVoiceAsync();
        var jobDto = await sut.StartAsync(
            book.Id,
            new SynthesisStartRequest(voice.Id, null, SadeceEksikler: true, Force: false),
            "trace-synth");

        var evt1 = new ChapterSynthesizedEvent
        {
            TraceId = "trace-synth",
            JobId = jobDto.Id,
            BookId = book.Id,
            ChapterId = chapters[0].Id,
            MetinSurumu = 1,
            OpusYolu = $"books/{book.Id}/audio/{chapters[0].Id}/v1/audio.ogg",
            Mp3Yolu = $"books/{book.Id}/audio/{chapters[0].Id}/v1/audio.mp3",
            ZamanDamgasiYolu = $"books/{book.Id}/audio/{chapters[0].Id}/v1/timestamps.json",
            OpusBoyutu = 120_000,
            Mp3Boyutu = 160_000,
            SureSn = 60.5,
            CumleSayisi = 12,
            Motor = "voxcpm2_modal",
            GpuMs = 15_000,
            OnbellekParca = 2,
            UretilenParca = 10
        };

        await sut.HandleChapterSynthesizedAsync(evt1);
        await sut.HandleChapterSynthesizedAsync(evt1); // idempotent 2nd delivery

        var jobAfterCh1 = await _jobRepository.GetByIdAsync(jobDto.Id);
        jobAfterCh1!.TamamlananBolum.Should().Be(1);
        jobAfterCh1.GpuMs.Should().Be(15_000);

        chapters[1].MetinSurumu = 2;
        _chapterRepository.Update(chapters[1]);
        await _chapterRepository.SaveChangesAsync();

        var evt2 = new ChapterSynthesizedEvent
        {
            TraceId = "trace-synth",
            JobId = jobDto.Id,
            BookId = book.Id,
            ChapterId = chapters[1].Id,
            MetinSurumu = 1,
            OpusYolu = $"books/{book.Id}/audio/{chapters[1].Id}/v1/audio.ogg",
            Mp3Yolu = $"books/{book.Id}/audio/{chapters[1].Id}/v1/audio.mp3",
            ZamanDamgasiYolu = $"books/{book.Id}/audio/{chapters[1].Id}/v1/timestamps.json",
            OpusBoyutu = 110_000,
            Mp3Boyutu = 150_000,
            SureSn = 55.0,
            CumleSayisi = 10,
            Motor = "voxcpm2_modal",
            GpuMs = 14_000,
            OnbellekParca = 0,
            UretilenParca = 10
        };

        await sut.HandleChapterSynthesizedAsync(evt2);

        var updatedCh2 = await _chapterRepository.GetByIdAsync(chapters[1].Id);
        updatedCh2!.SesDurumu.Should().Be(BolumSesDurumu.GuncelDegil);
    }

    [Fact]
    public async Task HandleCompletedAsync_AllChaptersReady_MarksBookHazirAndReleasesLock()
    {
        var sut = CreateSut();
        var (book, voice, chapters) = await SeedReadyBookAndVoiceAsync();
        var jobDto = await sut.StartAsync(
            book.Id,
            new SynthesisStartRequest(voice.Id, null, SadeceEksikler: true, Force: false),
            "trace-complete");

        foreach (var ch in chapters)
        {
            await sut.HandleChapterSynthesizedAsync(new ChapterSynthesizedEvent
            {
                TraceId = "trace-complete",
                JobId = jobDto.Id,
                BookId = book.Id,
                ChapterId = ch.Id,
                MetinSurumu = 1,
                OpusYolu = $"books/{book.Id}/audio/{ch.Id}/v1/audio.ogg",
                Mp3Yolu = $"books/{book.Id}/audio/{ch.Id}/v1/audio.mp3",
                ZamanDamgasiYolu = $"books/{book.Id}/audio/{ch.Id}/v1/timestamps.json",
                OpusBoyutu = 100_000,
                Mp3Boyutu = 140_000,
                SureSn = 50.0,
                CumleSayisi = 8,
                Motor = "voxcpm2_modal",
                GpuMs = 10_000,
                OnbellekParca = 0,
                UretilenParca = 8
            });
        }

        await sut.HandleCompletedAsync(new SynthesisCompletedEvent
        {
            TraceId = "trace-complete",
            JobId = jobDto.Id,
            BookId = book.Id,
            Sonuc = nameof(SentezIsDurumu.Tamamlandi),
            TamamlananBolum = 2,
            HataliBolum = 0,
            IptalEdildi = false,
            ToplamGpuMs = 20_000,
            UretilenSesSn = 100.0,
            KullanilanMotorlar = "voxcpm2_modal"
        });

        var reloadedBook = await _bookRepository.GetByIdAsync(book.Id);
        reloadedBook!.Durum.Should().Be(KitapDurumu.Hazir);

        var reloadedJob = await _jobRepository.GetByIdAsync(jobDto.Id);
        reloadedJob!.Durum.Should().Be(SentezIsDurumu.Tamamlandi);

        _cacheMock.Verify(x => x.RemoveAsync(CacheKeys.ActiveJob(book.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryAsync_PartiallyCompletedJob_StartsNewJobWithOnlyMissingChapters()
    {
        var sut = CreateSut();
        var (book, voice, chapters) = await SeedReadyBookAndVoiceAsync();
        var firstJob = await sut.StartAsync(
            book.Id,
            new SynthesisStartRequest(voice.Id, null, SadeceEksikler: true, Force: false),
            "trace-retry-1");

        await sut.HandleChapterSynthesizedAsync(new ChapterSynthesizedEvent
        {
            TraceId = "trace-retry-1",
            JobId = firstJob.Id,
            BookId = book.Id,
            ChapterId = chapters[0].Id,
            MetinSurumu = 1,
            OpusYolu = $"books/{book.Id}/audio/{chapters[0].Id}/v1/audio.ogg",
            Mp3Yolu = $"books/{book.Id}/audio/{chapters[0].Id}/v1/audio.mp3",
            ZamanDamgasiYolu = $"books/{book.Id}/audio/{chapters[0].Id}/v1/timestamps.json",
            OpusBoyutu = 100_000,
            Mp3Boyutu = 140_000,
            SureSn = 50.0,
            CumleSayisi = 8,
            Motor = "voxcpm2_modal",
            GpuMs = 10_000,
            OnbellekParca = 0,
            UretilenParca = 8
        });

        await sut.HandleErrorAsync(new PipelineErrorEvent
        {
            TraceId = "trace-retry-1",
            BookId = book.Id,
            JobId = firstJob.Id,
            ChapterId = chapters[1].Id,
            Asama = nameof(PipelineAsamasi.Sentez),
            HataKodu = "CHAPTER_FAILED",
            HataMesaji = "Bölüm seslendirilemedi.",
            Kurtarilabilir = true
        });

        await sut.HandleCompletedAsync(new SynthesisCompletedEvent
        {
            TraceId = "trace-retry-1",
            JobId = firstJob.Id,
            BookId = book.Id,
            Sonuc = nameof(SentezIsDurumu.KismenTamamlandi),
            TamamlananBolum = 1,
            HataliBolum = 1,
            IptalEdildi = false,
            ToplamGpuMs = 10_000,
            UretilenSesSn = 50.0,
            KullanilanMotorlar = "voxcpm2_modal"
        });

        var retriedJob = await sut.RetryAsync(firstJob.Id, "trace-retry-2");

        retriedJob.ToplamBolum.Should().Be(1);
    }
}
