using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SesliKitap.Data.Context;
using SesliKitap.IntegrationTests.Fixtures;

namespace SesliKitap.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class MigrationTests
{
    private readonly ContainersFixture _containers;

    public MigrationTests(ContainersFixture containers)
    {
        _containers = containers;
    }

    [Fact]
    public async Task DatabaseMigrations_ApplyCleanlyAndContainExpectedHistory()
    {
        await using var factory = new ApiFactory(_containers);
        using var client = factory.CreateAuthorizedClient();

        var healthResponse = await client.GetAsync(new Uri("/api/health", UriKind.Relative));
        healthResponse.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToList();

        appliedMigrations.Should().HaveCountGreaterThanOrEqualTo(4);
        appliedMigrations.Should().Contain(m => m.EndsWith("InitialCreate", StringComparison.Ordinal));
        appliedMigrations.Should().Contain(m => m.EndsWith("AddBooksAndChapters", StringComparison.Ordinal));
        appliedMigrations.Should().Contain(m => m.EndsWith("AddVoiceProfiles", StringComparison.Ordinal));
        appliedMigrations.Should().Contain(m => m.EndsWith("AddSynthesisJobs", StringComparison.Ordinal));
    }
}
