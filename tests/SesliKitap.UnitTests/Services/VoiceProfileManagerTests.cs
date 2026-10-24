using System.Text;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using SesliKitap.Business.DTOs.Voice;
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

namespace SesliKitap.UnitTests.Services;

/// <summary>
/// <see cref="VoiceProfileManager"/> için birim testleri (Faz 3).
/// </summary>
public sealed class VoiceProfileManagerTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var interceptor = new AuditingInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"seslikitap-voice-tests-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;
        return new AppDbContext(options);
    }

    private static FormFile CreateValidWavFormFile(string fileName = "referans.wav")
    {
        // Geçerli RIFF....WAVE başlığı (44 bayt)
        var bytes = new byte[128];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        BitConverter.GetBytes(120).CopyTo(bytes, 4);
        Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(bytes, 8);

        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "ses", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "audio/wav"
        };
    }

    private static VoiceProfileManager CreateSut(
        AppDbContext db,
        out Mock<IFileStorageService> storageMock,
        out Mock<IEventPublisher> publisherMock,
        out Mock<INotificationService> notificationsMock)
    {
        var repo = new VoiceProfileRepository(db);
        storageMock = new Mock<IFileStorageService>();
        publisherMock = new Mock<IEventPublisher>();
        notificationsMock = new Mock<INotificationService>();
        var logMock = new Mock<ILogService>();
        var cacheMock = new Mock<ICacheService>();

        cacheMock
            .Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<IReadOnlyList<VoiceProfileDto>>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<IReadOnlyList<VoiceProfileDto>>> factory, TimeSpan _, CancellationToken ct) =>
                factory(ct));

        var uploadOpts = Options.Create(new UploadOptions());
        var synthDefaults = Options.Create(new SynthesisDefaultsOptions());
        var cacheOpts = Options.Create(new CacheOptions());

        var createValidator = new VoiceProfileCreateRequestValidator(uploadOpts);
        var updateValidator = new VoiceProfileUpdateRequestValidator();

        return new VoiceProfileManager(
            repo,
            storageMock.Object,
            cacheMock.Object,
            publisherMock.Object,
            notificationsMock.Object,
            logMock.Object,
            createValidator,
            updateValidator,
            synthDefaults,
            cacheOpts);
    }

    [Fact]
    public async Task CreateAsync_ShouldReject_WhenConsentIsFalse()
    {
        await using var db = CreateInMemoryContext();
        var sut = CreateSut(db, out _, out _, out _);

        var req = new VoiceProfileCreateRequest
        {
            Ad = "Tok Anlatıcı",
            Ses = CreateValidWavFormFile(),
            KullanimOnayi = false
        };

        var act = async () => await sut.CreateAsync(req, "trace-1");
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*kullanimOnayi*");
    }

    [Fact]
    public async Task CreateAsync_ShouldUploadAndPublishEvent_AndRejectDuplicateName()
    {
        await using var db = CreateInMemoryContext();
        var sut = CreateSut(db, out var storageMock, out var publisherMock, out _);

        var req = new VoiceProfileCreateRequest
        {
            Ad = "Tok Anlatıcı",
            Aciklama = "Roman okumaları için",
            Cinsiyet = Cinsiyet.Erkek,
            ReferansMetin = "Merhaba dünya.",
            Ses = CreateValidWavFormFile(),
            KullanimOnayi = true
        };

        var dto = await sut.CreateAsync(req, "trace-2");

        dto.Ad.Should().Be("Tok Anlatıcı");
        dto.Durum.Should().Be(SesProfiliDurumu.Hazirlaniyor);
        dto.VarsayilanMi.Should().BeTrue("ilk eklenen profil otomatik varsayılan olur");

        storageMock.Verify(
            s => s.UploadAsync(
                It.Is<string>(k => k.StartsWith($"voices/{dto.Id:D}/raw.wav", StringComparison.Ordinal)),
                It.IsAny<Stream>(),
                It.IsAny<long>(),
                "audio/wav",
                It.IsAny<CancellationToken>()),
            Times.Once);

        publisherMock.Verify(
            p => p.PublishAsync(
                It.Is<VoiceProfileUploadedEvent>(e => e.VoiceProfileId == dto.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Aynı isimle ikinci kayıt -> 409 ConflictException
        var dupReq = new VoiceProfileCreateRequest
        {
            Ad = "tok anlatıcı",
            Ses = CreateValidWavFormFile(),
            KullanimOnayi = true
        };
        var actDup = async () => await sut.CreateAsync(dupReq, "trace-3");
        await actDup.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task HandleReadyAsync_ShouldBeIdempotent_AndSetDefaultCorrectly()
    {
        await using var db = CreateInMemoryContext();
        var sut = CreateSut(db, out _, out _, out var notificationsMock);

        var first = await sut.CreateAsync(
            new VoiceProfileCreateRequest
            {
                Ad = "Birinci Ses",
                Ses = CreateValidWavFormFile("s1.wav"),
                KullanimOnayi = true
            },
            "t1");

        var second = await sut.CreateAsync(
            new VoiceProfileCreateRequest
            {
                Ad = "İkinci Ses",
                Ses = CreateValidWavFormFile("s2.wav"),
                KullanimOnayi = true
            },
            "t2");

        var readyEvent = new VoiceProfileReadyEvent
        {
            VoiceProfileId = second.Id,
            Basarili = true,
            ReferansSesYolu = $"voices/{second.Id:D}/reference.wav",
            ReferansHash = new string('f', 64),
            ReferansSureSn = 12.4,
            OrnekSesYolu = $"voices/{second.Id:D}/sample.ogg",
            GpuMs = 1500
        };

        await sut.HandleReadyAsync(readyEvent);
        await sut.HandleReadyAsync(readyEvent); // İkinci çağrı idempotent olmalı

        var updatedSecond = await sut.GetByIdAsync(second.Id);
        updatedSecond.Durum.Should().Be(SesProfiliDurumu.Hazir);
        updatedSecond.OrnekSesVar.Should().BeTrue();

        var entityInDb = await db.VoiceProfiles.FindAsync(second.Id);
        entityInDb!.GpuMs.Should().Be(1500, "idempotent ikinci çağrı GPU süresini iki kez eklememeli");

        notificationsMock.Verify(
            n => n.VoiceProfileReadyAsync(second.Id, true, null, It.IsAny<CancellationToken>()),
            Times.Once);

        // İkinci sesi varsayılan yap -> birinci sesin varsayılanlığı kalkmalı
        await sut.SetDefaultAsync(second.Id);

        var afterFirst = await sut.GetByIdAsync(first.Id);
        var afterSecond = await sut.GetByIdAsync(second.Id);
        afterFirst.VarsayilanMi.Should().BeFalse();
        afterSecond.VarsayilanMi.Should().BeTrue();
    }
}
