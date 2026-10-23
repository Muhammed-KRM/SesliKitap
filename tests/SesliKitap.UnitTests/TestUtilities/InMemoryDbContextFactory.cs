using Microsoft.EntityFrameworkCore;
using SesliKitap.Data.Context;

namespace SesliKitap.UnitTests.TestUtilities;

public sealed class InMemoryDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public InMemoryDbContextFactory(string? dbName = null)
    {
        var name = dbName ?? $"SesliKitap_Test_{Guid.NewGuid():N}";
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .AddInterceptors(new AuditingInterceptor())
            .Options;
    }

    public AppDbContext CreateDbContext() => new(_options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new AppDbContext(_options));
}

public sealed class FailingDbContextFactory : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext()
        => throw new InvalidOperationException("Simulated DB connection failure.");

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Simulated DB connection failure.");
}
