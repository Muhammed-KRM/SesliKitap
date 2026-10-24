using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SesliKitap.Business.Constants;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Context;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.IntegrationTests.Fixtures;

namespace SesliKitap.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class ChapterAudioStreamingTests
{
    private readonly ContainersFixture _containers;

    public ChapterAudioStreamingTests(ContainersFixture containers)
    {
        _containers = containers;
    }

    [Fact]
    public async Task StreamAudio_SupportsRange206SuffixRange416AndMediaTokenAuthentication()
    {
        await using var factory = new ApiFactory(_containers);
        using var anonymousClient = factory.CreateClient();

        const int totalBytes = 1_048_576; // 1 MB
        var audioBytes = RandomNumberGenerator.GetBytes(totalBytes);

        var bookId = Guid.CreateVersion7();
        var chapterId = Guid.CreateVersion7();
        var opusKey = StorageKeys.ChapterAudio(bookId, chapterId, 1, "ogg");

        string mediaToken;

        using (var scope = factory.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tokenService = scope.ServiceProvider.GetRequiredService<IMediaTokenService>();

            using var uploadStream = new MemoryStream(audioBytes);
            await storage.UploadAsync(opusKey, uploadStream, totalBytes, "audio/ogg");

            db.Books.Add(new Book
            {
                Id = bookId,
                Baslik = "Ses Akış Test Kitabı",
                Yazar = "Test Yazar",
                Dil = "tr",
                KaynakTipi = KaynakTipi.Yukleme,
                DosyaFormati = DosyaFormati.Pdf,
                OrijinalDosyaAdi = "stream.pdf",
                OrijinalDosyaYolu = $"books/{bookId}/source.pdf",
                DosyaHash = new string('c', 64),
                Durum = KitapDurumu.Hazir
            });

            db.BookChapters.Add(new BookChapter
            {
                Id = chapterId,
                BookId = bookId,
                SiraNo = 1,
                Baslik = "Birinci Bölüm",
                TemizMetin = "Deneme metni.",
                KelimeSayisi = 2,
                KarakterSayisi = 13,
                MetinSurumu = 1,
                SesMetinSurumu = 1,
                SesDurumu = BolumSesDurumu.Hazir,
                OpusYolu = opusKey,
                OpusBoyutu = totalBytes,
                SureSn = 120.0
            });

            await db.SaveChangesAsync();
            (mediaToken, _) = tokenService.Create();
        }

        // 1. Medya token ve API anahtarı olmadan -> 401 Unauthorized
        var unauthorizedResp = await anonymousClient.GetAsync(
            new Uri($"/api/chapters/{chapterId}/audio?format=opus", UriKind.Relative));
        unauthorizedResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Geçerli medya token (?t=...) ve Range olmadan -> 200 OK tam dosya
        var fullUrl = $"/api/chapters/{chapterId}/audio?format=opus&t={Uri.EscapeDataString(mediaToken)}";
        var fullResp = await anonymousClient.GetAsync(new Uri(fullUrl, UriKind.Relative));
        var fullErrBody = fullResp.IsSuccessStatusCode ? string.Empty : await fullResp.Content.ReadAsStringAsync();
        fullResp.StatusCode.Should().Be(HttpStatusCode.OK, because: fullErrBody);
        fullResp.Headers.AcceptRanges.Should().Contain("bytes");
        fullResp.Content.Headers.ContentLength.Should().Be(totalBytes);

        // 3. Range: bytes=0-99 -> 206 Partial Content, Content-Range: bytes 0-99/1048576
        using var rangeReq1 = new HttpRequestMessage(HttpMethod.Get, new Uri(fullUrl, UriKind.Relative));
        rangeReq1.Headers.Range = new RangeHeaderValue(0, 99);
        var rangeResp1 = await anonymousClient.SendAsync(rangeReq1);

        rangeResp1.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        rangeResp1.Content.Headers.ContentRange.Should().NotBeNull();
        rangeResp1.Content.Headers.ContentRange!.From.Should().Be(0);
        rangeResp1.Content.Headers.ContentRange.To.Should().Be(99);
        rangeResp1.Content.Headers.ContentRange.Length.Should().Be(totalBytes);
        var slice1 = await rangeResp1.Content.ReadAsByteArrayAsync();
        slice1.Should().HaveCount(100);
        slice1.Should().Equal(audioBytes.AsSpan(0, 100).ToArray());

        // 4. Sonek aralığı: bytes=-100 (son 100 bayt) -> 206 Partial Content
        using var suffixReq = new HttpRequestMessage(HttpMethod.Get, new Uri(fullUrl, UriKind.Relative));
        suffixReq.Headers.TryAddWithoutValidation("Range", "bytes=-100");
        var suffixResp = await anonymousClient.SendAsync(suffixReq);

        suffixResp.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        var suffixSlice = await suffixResp.Content.ReadAsByteArrayAsync();
        suffixSlice.Should().HaveCount(100);
        suffixSlice.Should().Equal(audioBytes.AsSpan(totalBytes - 100, 100).ToArray());

        // 5. Karşılanamaz aralık: bytes=2000000- -> 416 Range Not Satisfiable
        using var invalidRangeReq = new HttpRequestMessage(HttpMethod.Get, new Uri(fullUrl, UriKind.Relative));
        invalidRangeReq.Headers.TryAddWithoutValidation("Range", "bytes=2000000-");
        var invalidRangeResp = await anonymousClient.SendAsync(invalidRangeReq);

        invalidRangeResp.StatusCode.Should().Be(HttpStatusCode.RequestedRangeNotSatisfiable);
    }
}
