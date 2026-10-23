using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SesliKitap.API.Middleware;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Services;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.Middleware;

public sealed class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int StatusCode, string Body, InMemoryDbContextFactory DbFactory)> ExecuteMiddlewareAsync(
        Exception exceptionToThrow,
        string environmentName = "Development")
    {
        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(x => x.EnvironmentName).Returns(environmentName);

        var dbFactory = new InMemoryDbContextFactory();
        var logService = new LogManager(dbFactory, new FakeClock(), NullLogger<LogManager>.Instance);

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exceptionToThrow,
            envMock.Object,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext
        {
            TraceIdentifier = "test-trace-500"
        };
        context.Request.Method = "POST";
        context.Request.Path = "/api/books/upload";
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await middleware.InvokeAsync(context, logService);

        var body = Encoding.UTF8.GetString(responseStream.ToArray());
        return (context.Response.StatusCode, body, dbFactory);
    }

    [Fact]
    public async Task InvokeAsync_WhenConflictExceptionThrown_ShouldReturn409WithExistingId()
    {
        var existingId = Guid.CreateVersion7();
        var (status, body, _) = await ExecuteMiddlewareAsync(
            new ConflictException("Bu kitap zaten kütüphanede mevcut.", existingId));

        status.Should().Be(StatusCodes.Status409Conflict);
        body.Should().Contain(existingId.ToString());
        body.Should().Contain("test-trace-500");
    }

    [Fact]
    public async Task InvokeAsync_WhenNotFoundExceptionThrown_ShouldReturn404()
    {
        var id = Guid.CreateVersion7();
        var (status, body, _) = await ExecuteMiddlewareAsync(new NotFoundException("Kitap", id));

        status.Should().Be(StatusCodes.Status404NotFound);
        body.Should().Contain("Kitap bulunamadı");
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledExceptionInProduction_ShouldHideInternalDetailsAndLogToDatabase()
    {
        var (status, body, dbFactory) = await ExecuteMiddlewareAsync(
            new InvalidOperationException("Gizli veritabanı tablo adı sızıntısı"),
            environmentName: "Production");

        status.Should().Be(StatusCodes.Status500InternalServerError);
        body.Should().NotContain("Gizli veritabanı tablo adı sızıntısı");
        body.Should().Contain("Beklenmeyen bir hata oluştu.");
        body.Should().Contain("test-trace-500");

        await using var db = await dbFactory.CreateDbContextAsync();
        var functionLog = await db.FunctionLogs.SingleAsync();
        functionLog.TraceId.Should().Be("test-trace-500");
        functionLog.ErrorMessage.Should().Contain("Gizli veritabanı tablo adı sızıntısı");
    }
}
