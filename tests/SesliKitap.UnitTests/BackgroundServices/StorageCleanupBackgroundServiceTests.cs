using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SesliKitap.API.BackgroundServices;
using SesliKitap.Business.Constants;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;
using SesliKitap.Data.Repositories;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.BackgroundServices;

public sealed class StorageCleanupBackgroundServiceTests
{
    [Fact]
    public async Task CleanupExpiredBooksOnceAsync_DeletesOnlySoftDeletedBooksOlderThan24Hours()
    {
        var dbFactory = new InMemoryDbContextFactory();
        var fakeClock = new FakeClock();
        var storageMock = new Mock<IFileStorageService>();
        var logMock = new Mock<ILogService>();

        var expiredBookId = Guid.CreateVersion7();
        var recentDeletedBookId = Guid.CreateVersion7();

        await using (var seedCtx = await dbFactory.CreateDbContextAsync())
        {
            seedCtx.Books.AddRange(
                new Book
                {
                    Id = expiredBookId,
                    Baslik = "Eski Silinmiş Kitap",
                    Durum = KitapDurumu.OnizlemeBekliyor,
                    DosyaFormati = DosyaFormati.Pdf,
                    KaynakTipi = KaynakTipi.Yukleme,
                    DosyaBoyutu = 1000,
                    SilindiMi = true,
                    SilinmeTarihi = fakeClock.UtcNow.UtcDateTime.AddHours(-25)
                },
                new Book
                {
                    Id = recentDeletedBookId,
                    Baslik = "Yeni Silinmiş Kitap",
                    Durum = KitapDurumu.OnizlemeBekliyor,
                    DosyaFormati = DosyaFormati.Pdf,
                    KaynakTipi = KaynakTipi.Yukleme,
                    DosyaBoyutu = 1000,
                    SilindiMi = true,
                    SilinmeTarihi = fakeClock.UtcNow.UtcDateTime.AddHours(-2)
                });
            await seedCtx.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddScoped(_ => dbFactory.CreateDbContext());
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddSingleton(storageMock.Object);
        services.AddSingleton(logMock.Object);
        using var provider = services.BuildServiceProvider();

        var sut = new StorageCleanupBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            fakeClock,
            NullLogger<StorageCleanupBackgroundService>.Instance);

        await sut.CleanupExpiredBooksOnceAsync();

        storageMock.Verify(
            s => s.DeleteByPrefixAsync(StorageKeys.BookPrefix(expiredBookId), It.IsAny<CancellationToken>()),
            Times.Once);
        storageMock.Verify(
            s => s.DeleteByPrefixAsync(StorageKeys.BookPrefix(recentDeletedBookId), It.IsAny<CancellationToken>()),
            Times.Never);

        await using var verifyCtx = await dbFactory.CreateDbContextAsync();
        var remaining = await verifyCtx.Books.IgnoreQueryFilters().Select(b => b.Id).ToListAsync();
        remaining.Should().ContainSingle().Which.Should().Be(recentDeletedBookId);
    }
}
