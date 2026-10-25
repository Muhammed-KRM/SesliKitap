using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SesliKitap.API.BackgroundServices;
using SesliKitap.Data.Context;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.Services;

public sealed class Faz9ServicesTests
{
    [Fact]
    public async Task LogRetentionBackgroundService_PurgesExpiredLogsAccordingToRetentionRules()
    {
        var now = new DateTimeOffset(2026, 10, 25, 3, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock { UtcNow = now };

        var factory = new InMemoryDbContextFactory();
        var services = new ServiceCollection();
        services.AddScoped<AppDbContext>(_ => factory.CreateDbContext());
        var provider = services.BuildServiceProvider();

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            // EndpointLog: 30 gün saklama -> 31 günlük silinmeli, 10 günlük kalmalı
            db.EndpointLogs.AddRange(
                new EndpointLog { Method = "GET", Path = "/api/old", StatusCode = 200, CreatedAt = now.UtcDateTime.AddDays(-31) },
                new EndpointLog { Method = "GET", Path = "/api/fresh", StatusCode = 200, CreatedAt = now.UtcDateTime.AddDays(-10) });

            // PipelineLog: 90 gün saklama -> 95 günlük silinmeli, 45 günlük kalmalı
            db.PipelineLogs.AddRange(
                new PipelineLog { Asama = "Sentez", Durum = "Tamamlandi", CreatedAt = now.UtcDateTime.AddDays(-95) },
                new PipelineLog { Asama = "Sentez", Durum = "Tamamlandi", CreatedAt = now.UtcDateTime.AddDays(-45) });

            // FunctionLog: 180 gün saklama -> 185 günlük silinmeli, 120 günlük kalmalı
            db.FunctionLogs.AddRange(
                new FunctionLog { ErrorCode = "OLD_ERR", ClassName = "Test", MethodName = "M", CreatedAt = now.UtcDateTime.AddDays(-185) },
                new FunctionLog { ErrorCode = "NEW_ERR", ClassName = "Test", MethodName = "M", CreatedAt = now.UtcDateTime.AddDays(-120) });

            await db.SaveChangesAsync();
        }

        var retentionService = new LogRetentionBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            clock,
            NullLogger<LogRetentionBackgroundService>.Instance);

        var (epDel, pipeDel, fnDel) = await retentionService.PurgeExpiredLogsOnceAsync();

        epDel.Should().Be(1);
        pipeDel.Should().Be(1);
        fnDel.Should().Be(1);

        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        verifyDb.EndpointLogs.Should().ContainSingle(x => x.Path == "/api/fresh");
        verifyDb.PipelineLogs.Should().ContainSingle();
        verifyDb.FunctionLogs.Should().ContainSingle(x => x.ErrorCode == "NEW_ERR");
    }

    [Fact]
    public async Task BookRepository_SearchWith1000BooksAndTurkishAccentFolding_CompletesUnder50Ms()
    {
        await using var db = new InMemoryDbContextFactory().CreateDbContext();
        var repo = new BookRepository(db);

        var books = new List<Book>(1000);
        for (var i = 0; i < 999; i++)
        {
            books.Add(new Book
            {
                Id = Guid.CreateVersion7(),
                Baslik = $"Örnek Roman Kitabı #{i}",
                Yazar = $"Yazar Adı {i}",
                Durum = KitapDurumu.Hazir,
                DosyaFormati = DosyaFormati.Epub,
                KaynakTipi = KaynakTipi.Yukleme
            });
        }

        books.Add(new Book
        {
            Id = Guid.CreateVersion7(),
            Baslik = "Çalıkuşu",
            Yazar = "Reşat Nuri Güntekin",
            Durum = KitapDurumu.Hazir,
            DosyaFormati = DosyaFormati.Epub,
            KaynakTipi = KaynakTipi.Yukleme
        });

        db.Books.AddRange(books);
        await db.SaveChangesAsync();

        // Isınma (JIT)
        _ = await repo.GetPagedListAsync("Roman", null, null, 1, 20);

        var sw = Stopwatch.StartNew();
        var (itemsAscii, totalAscii) = await repo.GetPagedListAsync("calikusu", null, null, 1, 20);
        sw.Stop();

        totalAscii.Should().Be(1);
        itemsAscii.Should().ContainSingle(x => x.Baslik == "Çalıkuşu");
        sw.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(50);

        var (itemsTr, totalTr) = await repo.GetPagedListAsync("çalıkuşu", null, null, 1, 20);
        totalTr.Should().Be(1);
        itemsTr.Should().ContainSingle(x => x.Baslik == "Çalıkuşu");
    }
}
