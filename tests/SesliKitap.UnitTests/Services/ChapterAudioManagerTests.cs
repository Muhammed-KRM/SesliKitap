using FluentAssertions;
using Moq;
using SesliKitap.Business.Constants;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Services;
using SesliKitap.Data.Context;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.Services;

public sealed class ChapterAudioManagerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ChapterRepository _chapterRepository;
    private readonly Mock<IFileStorageService> _storageMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly ChapterAudioManager _sut;

    public ChapterAudioManagerTests()
    {
        _db = new InMemoryDbContextFactory().CreateDbContext();
        _chapterRepository = new ChapterRepository(_db);
        _sut = new ChapterAudioManager(_chapterRepository, _storageMock.Object, _cacheMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task GetAudioInfoAsync_ReadyChapter_ReturnsDescriptorAndCachesMetadata()
    {
        var chapterId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var opusKey = $"books/{bookId}/audio/{chapterId}/v2/audio.ogg";
        var mp3Key = $"books/{bookId}/audio/{chapterId}/v2/audio.mp3";

        _db.BookChapters.Add(new BookChapter
        {
            Id = chapterId,
            BookId = bookId,
            SiraNo = 1,
            Baslik = "Bölüm 1",
            TemizMetin = "Merhaba.",
            KelimeSayisi = 1,
            KarakterSayisi = 8,
            MetinSurumu = 2,
            SesMetinSurumu = 2,
            SesDurumu = BolumSesDurumu.Hazir,
            OpusYolu = opusKey,
            OpusBoyutu = 65_536,
            Mp3Yolu = mp3Key,
            Mp3Boyutu = 98_304
        });
        await _db.SaveChangesAsync();

        _storageMock
            .Setup(x => x.GetInfoAsync(opusKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageObjectInfo(opusKey, 65_536, "\"etag-opus-1\"", "audio/ogg"));
        _storageMock
            .Setup(x => x.GetInfoAsync(mp3Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageObjectInfo(mp3Key, 98_304, "\"etag-mp3-1\"", "audio/mpeg"));

        var opusInfo = await _sut.GetAudioInfoAsync(chapterId, "opus");

        opusInfo.Length.Should().Be(65_536);
        opusInfo.ContentType.Should().Be("audio/ogg");
        opusInfo.ETag.Should().Be("\"etag-opus-1\"");

        var mp3Info = await _sut.GetAudioInfoAsync(chapterId, "mp3");
        mp3Info.Length.Should().Be(98_304);
        mp3Info.ContentType.Should().Be("audio/mpeg");
    }

    [Fact]
    public async Task GetAudioInfoAsync_ChapterWithoutAudio_ThrowsNotFoundException()
    {
        var chapterId = Guid.CreateVersion7();
        _db.BookChapters.Add(new BookChapter
        {
            Id = chapterId,
            BookId = Guid.CreateVersion7(),
            SiraNo = 1,
            Baslik = "Bölüm 1",
            TemizMetin = "Merhaba.",
            KelimeSayisi = 1,
            KarakterSayisi = 8,
            SesDurumu = BolumSesDurumu.Yok
        });
        await _db.SaveChangesAsync();

        var act = () => _sut.GetAudioInfoAsync(chapterId, "opus");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetTimestampsJsonAsync_LoadsFromStorageAndCachesInRedis()
    {
        var chapterId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var tsKey = $"books/{bookId}/audio/{chapterId}/v1/timestamps.json";
        var sampleJson = """{"version":1,"chapterId":"00000000-0000-0000-0000-000000000001","durationMs":1200,"sentences":[{"i":0,"p":0,"s":0,"e":1200,"t":"Merhaba dünya."}]}""";

        _db.BookChapters.Add(new BookChapter
        {
            Id = chapterId,
            BookId = bookId,
            SiraNo = 1,
            Baslik = "Bölüm 1",
            TemizMetin = "Merhaba dünya.",
            KelimeSayisi = 2,
            KarakterSayisi = 14,
            SesDurumu = BolumSesDurumu.Hazir,
            ZamanDamgasiYolu = tsKey
        });
        await _db.SaveChangesAsync();

        _storageMock
            .Setup(x => x.ReadTextAsync(tsKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleJson);

        var json = await _sut.GetTimestampsJsonAsync(chapterId);

        json.Should().Be(sampleJson);
        _cacheMock.Verify(
            x => x.SetStringAsync(CacheKeys.ChapterTimestamps(chapterId), sampleJson, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
