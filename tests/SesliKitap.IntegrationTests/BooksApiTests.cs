using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Book;
using SesliKitap.Business.Events;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Enums;
using SesliKitap.IntegrationTests.Fixtures;

namespace SesliKitap.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class BooksApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly ContainersFixture _containers;

    public BooksApiTests(ContainersFixture containers)
    {
        _containers = containers;
    }

    [Fact]
    public async Task UploadPdf_StoresInMinioPublishesEventAndRejectsDuplicateWith409()
    {
        await using var factory = new ApiFactory(_containers);
        using var client = factory.CreateAuthorizedClient();
        var harness = factory.Services.GetRequiredService<ITestHarness>();

        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\nIntegration Test PDF\n%%EOF");

        using var form1 = CreateUploadMultipart(pdfBytes, "entegrasyon_kitabi.pdf", "Entegrasyon Test Kitabı", "Test Yazar");
        var response1 = await client.PostAsync(new Uri("/api/books/upload", UriKind.Relative), form1);

        response1.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var createdBook = await response1.Content.ReadFromJsonAsync<BookDetailDto>(JsonOptions);
        createdBook.Should().NotBeNull();
        createdBook!.Baslik.Should().Be("Entegrasyon Test Kitabı");
        createdBook.Durum.Should().Be(KitapDurumu.Isleniyor);

        // MinIO'da kaynak dosyanın varlığını doğrula
        using (var scope = factory.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            var info = await storage.GetInfoAsync(StorageKeys.BookSource(createdBook.Id, "pdf"));
            info.Should().NotBeNull();
            info!.Size.Should().Be(pdfBytes.Length);
        }

        // MassTransit harness üzerinden BookImportRequestedEvent yayınlandığını doğrula
        var published = await harness.Published.Any<BookImportRequestedEvent>(
            x => x.Context.Message.BookId == createdBook.Id);
        published.Should().BeTrue();

        // Aynı içeriği ikinci kez yükleme -> 409 Conflict + existingId
        using var form2 = CreateUploadMultipart(pdfBytes, "kopya_kitap.pdf", "Kopya", null);
        var response2 = await client.PostAsync(new Uri("/api/books/upload", UriKind.Relative), form2);

        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problemJson = await response2.Content.ReadAsStringAsync();
        problemJson.Should().Contain(createdBook.Id.ToString());
    }

    [Fact]
    public async Task UploadDisguisedJpegAsPdf_Returns400BadRequest()
    {
        await using var factory = new ApiFactory(_containers);
        using var client = factory.CreateAuthorizedClient();

        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        using var form = CreateUploadMultipart(jpegBytes, "sahte.pdf", null, null);

        var response = await client.PostAsync(new Uri("/api/books/upload", UriKind.Relative), form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static MultipartFormDataContent CreateUploadMultipart(
        byte[] fileBytes,
        string fileName,
        string? title,
        string? author)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "Dosya", fileName);

        if (!string.IsNullOrWhiteSpace(title))
        {
            form.Add(new StringContent(title), "Baslik");
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            form.Add(new StringContent(author), "Yazar");
        }

        return form;
    }
}
