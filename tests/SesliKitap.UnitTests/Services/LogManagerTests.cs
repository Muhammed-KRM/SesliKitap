using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Logs;
using SesliKitap.Business.Services;
using SesliKitap.Data.Entities;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.Services;

public sealed class LogManagerTests
{
    [Fact]
    public async Task LogEndpointAsync_ShouldMaskSensitiveFieldsAndPersist()
    {
        var dbFactory = new InMemoryDbContextFactory();
        var clock = new FakeClock();
        var sut = new LogManager(dbFactory, clock, NullLogger<LogManager>.Instance);

        await sut.LogEndpointAsync(new EndpointLog
        {
            TraceId = "trace-1",
            Method = "POST",
            Path = "/api/auth/verify",
            Query = "?t=GIZLI_MEDYA_TOKEN&sayfa=1",
            RequestBody = "{\"apiKey\": \"SUPER_SECRET_KEY\"}",
            ResponseBody = "{\"token\": \"TOKEN_DEGERI\"}",
            StatusCode = 200,
            DurationMs = 18
        });

        await using var db = await dbFactory.CreateDbContextAsync();
        var saved = await db.EndpointLogs.SingleAsync();

        saved.Query.Should().Be("?t=***&sayfa=1");
        saved.RequestBody.Should().NotContain("SUPER_SECRET_KEY");
        saved.ResponseBody.Should().NotContain("TOKEN_DEGERI");
        saved.DurationMs.Should().Be(18);
    }

    [Fact]
    public async Task LogFunctionErrorAndWarning_ShouldCaptureCallerMetadataAndMaskInput()
    {
        var dbFactory = new InMemoryDbContextFactory();
        var clock = new FakeClock();
        var sut = new LogManager(dbFactory, clock, NullLogger<LogManager>.Instance);

        await sut.LogFunctionErrorAsync(
            ErrorCodes.BookUploadError,
            new InvalidOperationException("Yükleme hatası, X-API-Key: gizli_anahtar"),
            input: new { DosyaAdi = "kitap.pdf", apiKey = "gizli_anahtar" },
            traceId: "trace-err-1");

        await sut.LogFunctionWarningAsync(
            ErrorCodes.CacheAccessWarning,
            "Redis gecikmesi algılandı",
            traceId: "trace-warn-1");

        await using var db = await dbFactory.CreateDbContextAsync();
        var logs = await db.FunctionLogs.OrderBy(x => x.Id).ToListAsync();

        logs.Should().HaveCount(2);

        var errLog = logs[0];
        errLog.ErrorCode.Should().Be(ErrorCodes.BookUploadError);
        errLog.Severity.Should().Be("Error");
        errLog.ClassName.Should().Be(nameof(LogManagerTests));
        errLog.MethodName.Should().Be(nameof(LogFunctionErrorAndWarning_ShouldCaptureCallerMetadataAndMaskInput));
        errLog.LineNumber.Should().BeGreaterThan(0);
        errLog.ErrorMessage.Should().NotContain("gizli_anahtar");
        errLog.InputValue.Should().NotContain("gizli_anahtar");
        errLog.InputValue.Should().Contain("kitap.pdf");

        var warnLog = logs[1];
        warnLog.Severity.Should().Be("Warning");
        warnLog.ErrorCode.Should().Be(ErrorCodes.CacheAccessWarning);
    }

    [Fact]
    public async Task LogPipelineStartAndEnd_ShouldCalculateDurationMsAndUpdateStatus()
    {
        var dbFactory = new InMemoryDbContextFactory();
        var clock = new FakeClock();
        var sut = new LogManager(dbFactory, clock, NullLogger<LogManager>.Instance);

        var bookId = Guid.CreateVersion7();
        var logId = await sut.LogPipelineStartAsync(
            new PipelineLogContext(BookId: bookId, TraceId: "trace-pipe-1"),
            asama: "MetinCikarma",
            girdiMetadata: "{\"sayfaSayisi\": 120}");

        logId.Should().BeGreaterThan(0);

        clock.Advance(TimeSpan.FromMilliseconds(1450));
        await sut.LogPipelineEndAsync(logId, ciktiMetadata: "{\"bolumSayisi\": 12}");

        await using var db = await dbFactory.CreateDbContextAsync();
        var saved = await db.PipelineLogs.SingleAsync();

        saved.BookId.Should().Be(bookId);
        saved.Durum.Should().Be("Tamamlandi");
        saved.Yuzde.Should().Be(100);
        saved.SureMs.Should().Be(1450);
        saved.CiktiMetadata.Should().Be("{\"bolumSayisi\": 12}");
    }

    [Fact]
    public async Task LogPipelineEventAsync_ShouldFilterOutNon25PercentIntermediateProgress()
    {
        var dbFactory = new InMemoryDbContextFactory();
        var clock = new FakeClock();
        var sut = new LogManager(dbFactory, clock, NullLogger<LogManager>.Instance);

        await sut.LogPipelineEventAsync(new PipelineLogRequest
        {
            Asama = "Sentez",
            Durum = "Devam",
            Yuzde = 13,
            Mesaj = "Ara ilerleme %13"
        });

        await sut.LogPipelineEventAsync(new PipelineLogRequest
        {
            Asama = "Sentez",
            Durum = "Devam",
            Yuzde = 25,
            Mesaj = "Çeyrek ilerleme %25"
        });

        await using var db = await dbFactory.CreateDbContextAsync();
        var logs = await db.PipelineLogs.ToListAsync();

        logs.Should().ContainSingle();
        logs[0].Yuzde.Should().Be(25);
    }

    [Fact]
    public async Task AllLogMethods_WhenDatabaseFails_ShouldNeverThrowException()
    {
        var failingFactory = new FailingDbContextFactory();
        var clock = new FakeClock();
        var sut = new LogManager(failingFactory, clock, NullLogger<LogManager>.Instance);

        var act = async () =>
        {
            await sut.LogEndpointAsync(new EndpointLog { Method = "GET", Path = "/api/health" });
            var id = await sut.LogPipelineStartAsync(new PipelineLogContext(), "Test");
            id.Should().Be(0);
            await sut.LogPipelineEndAsync(1);
            await sut.LogPipelineErrorAsync(1, new InvalidOperationException("err"));
            await sut.LogPipelineEventAsync(new PipelineLogRequest { Asama = "Test", Durum = "Basladi" });
            await sut.LogFunctionErrorAsync("ERR", new InvalidOperationException("err"));
            await sut.LogFunctionWarningAsync("WARN", "warn");
        };

        await act.Should().NotThrowAsync();
    }
}
