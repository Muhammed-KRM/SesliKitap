using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SesliKitap.Business.DTOs.Book;
using SesliKitap.Business.DTOs.Progress;
using SesliKitap.Data.Context;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.IntegrationTests.Fixtures;

namespace SesliKitap.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class ProgressApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly ContainersFixture _containers;

    public ProgressApiTests(ContainersFixture containers)
    {
        _containers = containers;
    }

    [Fact]
    public async Task PutProgress_LastWriteWins_EnforcedAtomicallyAndContinueShelfWorks()
    {
        await using var factory = new ApiFactory(_containers);
        using var client = factory.CreateAuthorizedClient();

        var bookId = Guid.CreateVersion7();
        var chapterId = Guid.CreateVersion7();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Books.Add(new Book
            {
                Id = bookId,
                Baslik = "İlerleme Entegrasyon Kitabı",
                Yazar = "Test Yazar",
                Durum = KitapDurumu.Hazir,
                ToplamSureSn = 400
            });
            db.BookChapters.Add(new BookChapter
            {
                Id = chapterId,
                BookId = bookId,
                SiraNo = 1,
                Baslik = "Bölüm 1",
                TemizMetin = "Örnek bölüm metni.",
                KelimeSayisi = 800,
                DahilMi = true,
                SesDurumu = BolumSesDurumu.Hazir,
                SureSn = 400
            });
            await db.SaveChangesAsync();
        }

        // 1) İlk başta konum yok -> 204 NoContent
        var initialGet = await client.GetAsync(new Uri($"/api/books/{bookId}/progress", UriKind.Relative));
        initialGet.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var tNewer = DateTime.UtcNow.AddSeconds(-5);
        var tOlder = DateTime.UtcNow.AddMinutes(-3);

        // 2) Yeni zaman damgalı konum gönder (200 sn = %50)
        var putNewer = await client.PutAsJsonAsync(
            new Uri($"/api/books/{bookId}/progress", UriKind.Relative),
            new ProgressUpdateRequest
            {
                ChapterId = chapterId,
                PozisyonSn = 200,
                OynatmaHizi = 1.5,
                CihazKimligi = "pc-chrome",
                CihazAdi = "Windows · Chrome",
                IstemciZamani = tNewer
            });

        putNewer.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtoNewer = await putNewer.Content.ReadFromJsonAsync<ProgressDto>(JsonOptions);
        dtoNewer.Should().NotBeNull();
        dtoNewer!.PozisyonSn.Should().Be(200);
        dtoNewer.TamamlanmaYuzdesi.Should().Be(50.0);

        // 3) Çevrimdışı kuyruktan geç gelen daha eski zaman damgalı PUT (50 sn) yeni konumu ezmemeli!
        var putOlder = await client.PutAsJsonAsync(
            new Uri($"/api/books/{bookId}/progress", UriKind.Relative),
            new ProgressUpdateRequest
            {
                ChapterId = chapterId,
                PozisyonSn = 50,
                OynatmaHizi = 1.0,
                CihazKimligi = "android-phone",
                CihazAdi = "Android · Chrome",
                IstemciZamani = tOlder
            });

        putOlder.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtoAfterOlder = await putOlder.Content.ReadFromJsonAsync<ProgressDto>(JsonOptions);
        dtoAfterOlder.Should().NotBeNull();
        dtoAfterOlder!.PozisyonSn.Should().Be(200);
        dtoAfterOlder.CihazKimligi.Should().Be("pc-chrome");

        // 4) "Dinlemeye devam et" rafında görünmeli
        var continueResp = await client.GetAsync(new Uri("/api/books/continue?adet=10", UriKind.Relative));
        continueResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var continueBooks = await continueResp.Content.ReadFromJsonAsync<List<BookListDto>>(JsonOptions);
        continueBooks.Should().NotBeNull();
        continueBooks!.Should().Contain(b => b.Id == bookId && b.TamamlanmaYuzdesi == 50.0);
    }
}
