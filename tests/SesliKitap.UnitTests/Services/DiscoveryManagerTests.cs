using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Book;
using SesliKitap.Business.DTOs.Discovery;
using SesliKitap.Business.Infrastructure.Discovery;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;
using SesliKitap.Business.Services;
using SesliKitap.Business.Validators;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;

namespace SesliKitap.UnitTests.Services;

public sealed class DiscoveryManagerTests
{
    private const string SampleIaSearchJson = """
    {
      "response": {
        "docs": [
          {
            "identifier": "kaagisi_omer_seyfettin",
            "title": "Kaşağı",
            "creator": ["Ömer Seyfettin"],
            "year": "1919",
            "downloads": 1540,
            "format": ["EPUB", "Text PDF"]
          }
        ]
      }
    }
    """;

    private const string SampleOlSearchJson = """
    {
      "docs": [
        {
          "key": "/works/OL123W",
          "title": "Kaşağı",
          "author_name": ["Ömer Seyfettin"],
          "first_publish_year": 1919,
          "cover_i": 998877,
          "ia": ["kaagisi_omer_seyfettin"],
          "ebook_access": "public"
        }
      ]
    }
    """;

    private const string SampleGutendexJson = """
    {
      "results": [
        {
          "id": 54321,
          "title": "Yalnız Efe",
          "authors": [{ "name": "Ömer Seyfettin" }],
          "download_count": 820,
          "formats": {
            "application/epub+zip": "https://www.gutenberg.org/ebooks/54321.epub.images",
            "image/jpeg": "https://www.gutenberg.org/cache/epub/54321/pg54321.cover.medium.jpg"
          }
        }
      ]
    }
    """;

    [Fact]
    public async Task Providers_ParseRecordedJsonCorrectly_AndDeduplicateAcrossSources()
    {
        var iaClient = new HttpClient(new FakeJsonHandler(SampleIaSearchJson))
        {
            BaseAddress = new Uri("https://archive.org/")
        };
        var olClient = new HttpClient(new FakeJsonHandler(SampleOlSearchJson))
        {
            BaseAddress = new Uri("https://openlibrary.org/")
        };
        var gutClient = new HttpClient(new FakeJsonHandler(SampleGutendexJson))
        {
            BaseAddress = new Uri("https://gutendex.com/")
        };

        var iaProvider = new InternetArchiveSearchProvider(iaClient);
        var olProvider = new OpenLibrarySearchProvider(olClient, iaProvider);
        var gutProvider = new GutendexSearchProvider(gutClient);

        var bookRepoMock = new Mock<IBookRepository>();
        bookRepoMock
            .Setup(r => r.GetExistingSourceMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<(KaynakTipi, string), Guid>());

        var cacheMock = new Mock<ICacheService>();
        var logMock = new Mock<ILogService>();
        var opts = Options.Create(new DiscoveryOptions { ProviderTimeoutSec = 5, ResultCacheHours = 24 });

        var manager = new DiscoveryManager(
            [iaProvider, olProvider, gutProvider],
            bookRepoMock.Object,
            cacheMock.Object,
            logMock.Object,
            opts);

        var results = await manager.SearchAsync("Ömer Seyfettin", null, 1, "trace-1");

        // IA ve Open Library'deki "Kaşağı — Ömer Seyfettin" tek kartta birleşmeli, Gutenberg "Yalnız Efe" ayrı kalmalı
        results.Should().HaveCount(2);
        var kasagi = results.Single(r => r.Baslik == "Kaşağı");
        kasagi.Kaynaklar.Should().HaveCount(2);
        kasagi.Kaynaklar.Select(k => k.SaglayiciKodu).Should().Contain(["ia", "ol"]);
    }

    [Fact]
    public async Task SearchAsync_WhenOneProviderTimesOutOrFails_ReturnsOthersAndLogsWarning()
    {
        var goodProviderMock = new Mock<IBookSearchProvider>();
        goodProviderMock.SetupGet(p => p.Code).Returns("ia");
        goodProviderMock.SetupGet(p => p.DisplayName).Returns("Internet Archive");
        goodProviderMock.SetupGet(p => p.SourceType).Returns(KaynakTipi.InternetArchive);
        goodProviderMock.SetupGet(p => p.IsEnabled).Returns(true);
        goodProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DiscoveryResultDto(
                    "ia",
                    KaynakTipi.InternetArchive,
                    "id-1",
                    "Beyaz Lale",
                    "Ömer Seyfettin",
                    1914,
                    "tr",
                    null,
                    null,
                    100,
                    [DosyaFormati.Epub],
                    [],
                    [new DiscoverySourceBadgeDto("ia", KaynakTipi.InternetArchive, "id-1", null)],
                    0)
            ]);

        var failingProviderMock = new Mock<IBookSearchProvider>();
        failingProviderMock.SetupGet(p => p.Code).Returns("gutenberg");
        failingProviderMock.SetupGet(p => p.DisplayName).Returns("Gutenberg");
        failingProviderMock.SetupGet(p => p.SourceType).Returns(KaynakTipi.Gutenberg);
        failingProviderMock.SetupGet(p => p.IsEnabled).Returns(true);
        failingProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Provider timed out"));

        var bookRepoMock = new Mock<IBookRepository>();
        bookRepoMock
            .Setup(r => r.GetExistingSourceMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<(KaynakTipi, string), Guid>());

        var cacheMock = new Mock<ICacheService>();
        var logMock = new Mock<ILogService>();
        var opts = Options.Create(new DiscoveryOptions { ProviderTimeoutSec = 2, ResultCacheHours = 24 });

        var manager = new DiscoveryManager(
            [goodProviderMock.Object, failingProviderMock.Object],
            bookRepoMock.Object,
            cacheMock.Object,
            logMock.Object,
            opts);

        var results = await manager.SearchAsync("Beyaz Lale", null, 1, "trace-timeout");

        results.Should().ContainSingle();
        results[0].Baslik.Should().Be("Beyaz Lale");

        logMock.Verify(
            l => l.LogFunctionWarningAsync(
                ErrorCodes.DiscoveryProviderError,
                It.IsAny<string>(),
                It.IsAny<object?>(),
                "trace-timeout",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("http://127.0.0.1:5201/api/health", false)]
    [InlineData("http://localhost:9010/seslikitap/secret", false)]
    [InlineData("http://169.254.169.254/latest/meta-data/", false)]
    [InlineData("http://minio:9000/seslikitap/file.pdf", false)]
    [InlineData("ftp://archive.org/download/book/book.epub", false)]
    [InlineData("https://evil.example.com/book.pdf", false)]
    [InlineData("https://archive.org/download/id123/kitap.epub", true)]
    [InlineData("https://www.gutenberg.org/ebooks/123.epub.images", true)]
    public async Task BookImportRequestValidator_ValidatesTrustedDomainsAndBlocksSsrf(string url, bool expectedValid)
    {
        var opts = Options.Create(new DiscoveryOptions
        {
            WebSearch = new WebSearchOptions { Enabled = false }
        });
        var validator = new BookImportRequestValidator(opts);

        var req = new BookImportRequest(
            KaynakTipi.InternetArchive,
            "id-123",
            url,
            DosyaFormati.Epub,
            "Test Kitabı",
            "Yazar");

        var res = await validator.ValidateAsync(req);
        res.IsValid.Should().Be(expectedValid);
    }

    private sealed class FakeJsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(resp);
        }
    }
}
