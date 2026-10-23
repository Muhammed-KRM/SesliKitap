using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SesliKitap.API.Consumers;
using SesliKitap.Business.Events;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Services;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;

namespace SesliKitap.UnitTests.Consumers;

public sealed class BookConsumersTests
{
    private static BookProcessingResultHandler CreateHandler(
        Mock<IBookRepository> bookRepoMock,
        Mock<INotificationService> notificationMock,
        Mock<ILogService> logMock) =>
        new(
            bookRepoMock.Object,
            new Mock<IChapterRepository>().Object,
            new Mock<IFileStorageService>().Object,
            new Mock<ICacheService>().Object,
            notificationMock.Object,
            logMock.Object,
            NullLogger<BookProcessingResultHandler>.Instance);

    [Fact]
    public async Task BookProcessedConsumer_WhenHandlerThrows_LogsFunctionErrorAndRethrows()
    {
        var bookRepoMock = new Mock<IBookRepository>();
        var notificationMock = new Mock<INotificationService>();
        var logMock = new Mock<ILogService>();

        var bookId = Guid.CreateVersion7();
        bookRepoMock.Setup(r => r.GetByIdAsync(bookId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));

        var handler = CreateHandler(bookRepoMock, notificationMock, logMock);
        var consumer = new BookProcessedConsumer(handler, logMock.Object);
        var contextMock = new Mock<ConsumeContext<BookProcessedEvent>>();
        contextMock.Setup(c => c.Message).Returns(new BookProcessedEvent
        {
            BookId = bookId,
            BolumlerYolu = $"books/{bookId}/chapters.json",
            TraceId = "trace-err-1"
        });
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        var act = () => consumer.Consume(contextMock.Object);
        await act.Should().ThrowAsync<InvalidOperationException>();

        logMock.Verify(
            l => l.LogFunctionErrorAsync(
                It.IsAny<string>(),
                It.IsAny<Exception>(),
                It.IsAny<object?>(),
                "trace-err-1",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PipelineStageChangedConsumer_DelegatesToHandler()
    {
        var bookRepoMock = new Mock<IBookRepository>();
        var notificationMock = new Mock<INotificationService>();
        var logMock = new Mock<ILogService>();

        var handler = CreateHandler(bookRepoMock, notificationMock, logMock);
        var consumer = new PipelineStageChangedConsumer(handler, logMock.Object);
        var bookId = Guid.CreateVersion7();
        var contextMock = new Mock<ConsumeContext<PipelineStageChangedEvent>>();
        contextMock.Setup(c => c.Message).Returns(new PipelineStageChangedEvent
        {
            BookId = bookId,
            Asama = nameof(PipelineAsamasi.Temizleme),
            Yuzde = 65,
            Mesaj = "Temizleniyor",
            TraceId = "trace-stage"
        });
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        notificationMock.Verify(
            n => n.BookProcessingProgressAsync(bookId, nameof(PipelineAsamasi.Temizleme), 65, "Temizleniyor", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PipelineErrorConsumer_UpdatesBookStatusAndNotifies()
    {
        var bookRepoMock = new Mock<IBookRepository>();
        var notificationMock = new Mock<INotificationService>();
        var logMock = new Mock<ILogService>();

        var handler = CreateHandler(bookRepoMock, notificationMock, logMock);
        var consumer = new PipelineErrorConsumer(handler, logMock.Object);
        var bookId = Guid.CreateVersion7();
        var contextMock = new Mock<ConsumeContext<PipelineErrorEvent>>();
        contextMock.Setup(c => c.Message).Returns(new PipelineErrorEvent
        {
            BookId = bookId,
            Asama = nameof(PipelineAsamasi.MetinCikarma),
            HataKodu = "SK-PIPE-301",
            HataMesaji = "Bu PDF şifreli.",
            Kurtarilabilir = false,
            TraceId = "trace-err-2"
        });
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        bookRepoMock.Verify(
            r => r.UpdateStatusAsync(bookId, KitapDurumu.Hata, "Bu PDF şifreli.", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
