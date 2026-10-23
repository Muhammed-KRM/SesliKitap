using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Book;
using SesliKitap.Business.Events;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;
using SesliKitap.Business.Services;
using SesliKitap.Business.Validators;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.Services;

public sealed class BookManagerTests
{
    private readonly InMemoryDbContextFactory _dbFactory = new();
    private readonly Mock<IFileStorageService> _storageMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IEventPublisher> _publisherMock = new();
    private readonly Mock<INotificationService> _notificationsMock = new();
    private readonly Mock<ILogService> _logMock = new();

    private BookManager CreateSut()
    {
        var context = _dbFactory.CreateDbContext();
        var bookRepo = new BookRepository(context);
        var chapterRepo = new ChapterRepository(context);
        var uploadOptions = Options.Create(new UploadOptions());
        var cacheOptions = Options.Create(new CacheOptions());

        return new BookManager(
            bookRepo,
            chapterRepo,
            _storageMock.Object,
            _cacheMock.Object,
            _publisherMock.Object,
            _notificationsMock.Object,
            _logMock.Object,
            new BookUploadRequestValidator(uploadOptions),
            new BookUpdateRequestValidator(),
            cacheOptions);
    }

    [Fact]
    public async Task UploadAsync_ValidPdf_UploadsToStoragePublishesEventAndReturnsIsleniyor()
    {
        var sut = CreateSut();
        var file = FormFileBuilder.CreatePdf("Kürk_Mantolu_Madonna.pdf", "Roman icerigi");
        var req = new BookUploadRequest { Dosya = file, Yazar = "Sabahattin Ali" };

        var result = await sut.UploadAsync(req, "trace-upload-1");

        result.Baslik.Should().Be("Kürk Mantolu Madonna");
        result.Yazar.Should().Be("Sabahattin Ali");
        result.Durum.Should().Be(KitapDurumu.Isleniyor);
        result.DosyaFormati.Should().Be(DosyaFormati.Pdf);

        _storageMock.Verify(
            s => s.UploadAsync(
                It.Is<string>(k => k.EndsWith("/source/original.pdf", StringComparison.Ordinal)),
                It.IsAny<Stream>(),
                file.Length,
                "application/pdf",
                It.IsAny<CancellationToken>()),
            Times.Once);

        _publisherMock.Verify(
            p => p.PublishAsync(
                It.Is<BookImportRequestedEvent>(e => e.BookId == result.Id && e.BeklenenFormat == "pdf"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadAsync_DuplicateFileHash_ThrowsConflictExceptionWithExistingBookId()
    {
        var sut1 = CreateSut();
        var file1 = FormFileBuilder.CreatePdf("kitap1.pdf", "Ayni icerik");
        var first = await sut1.UploadAsync(new BookUploadRequest { Dosya = file1 }, "trace-1");

        var sut2 = CreateSut();
        var file2 = FormFileBuilder.CreatePdf("farkli_isim.pdf", "Ayni icerik");

        var act = async () => await sut2.UploadAsync(new BookUploadRequest { Dosya = file2 }, "trace-2");

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ExistingId.Should().Be(first.Id);
    }

    [Fact]
    public async Task UploadAsync_DisguisedJpegAsPdf_ThrowsBusinessException()
    {
        var sut = CreateSut();
        var fakePdf = FormFileBuilder.CreateDisguisedJpegAsPdf("sahte.pdf");

        var act = async () => await sut.UploadAsync(new BookUploadRequest { Dosya = fakePdf }, "trace-bad");

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*uzantıyla uyuşmuyor*");
    }

    [Fact]
    public async Task UploadAsync_WhenEventPublishFails_SetsBookStatusToHataAndThrowsExternalServiceException()
    {
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<BookImportRequestedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("RabbitMQ down"));

        var sut = CreateSut();
        var file = FormFileBuilder.CreatePdf("hata.pdf", "Hata testi");

        var act = async () => await sut.UploadAsync(new BookUploadRequest { Dosya = file }, "trace-rmq-fail");

        await act.Should().ThrowAsync<ExternalServiceException>();

        await using var verifyCtx = await _dbFactory.CreateDbContextAsync();
        var savedBook = verifyCtx.Books.Single();
        savedBook.Durum.Should().Be(KitapDurumu.Hata);
        savedBook.HataMesaji.Should().Contain("Yeniden işle");
    }

    [Fact]
    public async Task DeleteAsync_WhenActiveJobExists_SetsCancelFlagAndSoftDeletesBook()
    {
        var sut = CreateSut();
        var file = FormFileBuilder.CreatePdf("silinecek.pdf", "Silme testi");
        var created = await sut.UploadAsync(new BookUploadRequest { Dosya = file }, "trace-del");

        var activeJobId = Guid.CreateVersion7();
        _cacheMock
            .Setup(c => c.GetStringAsync(CacheKeys.ActiveJob(created.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeJobId.ToString());

        var sutDelete = CreateSut();
        await sutDelete.DeleteAsync(created.Id);

        _cacheMock.Verify(
            c => c.SetStringAsync(CacheKeys.JobCancel(activeJobId), "1", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var act = async () => await CreateSut().GetByIdAsync(created.Id);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
