using FluentAssertions;
using Microsoft.Extensions.Options;
using SesliKitap.Business.Options;
using SesliKitap.Business.Services;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.Services;

public sealed class MediaTokenManagerTests
{
    private static MediaTokenManager CreateSut(FakeClock clock, int lifetimeHours = 6)
    {
        var opts = Options.Create(new SecurityOptions
        {
            ApiKey = "0123456789abcdef0123456789abcdef0123456789abcdef",
            MediaTokenSecret = "media_secret_0123456789abcdef0123456789abcdef",
            MediaTokenLifetimeHours = lifetimeHours
        });
        return new MediaTokenManager(opts, clock);
    }

    [Fact]
    public void CreateAndValidate_WhenWithinLifetime_ShouldReturnTrue()
    {
        var clock = new FakeClock();
        var sut = CreateSut(clock, lifetimeHours: 6);

        var (token, expiresAt) = sut.Create();

        expiresAt.Should().Be(clock.UtcNow.AddHours(6));
        sut.Validate(token).Should().BeTrue();

        clock.Advance(TimeSpan.FromHours(5));
        sut.Validate(token).Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenExpired_ShouldReturnFalse()
    {
        var clock = new FakeClock();
        var sut = CreateSut(clock, lifetimeHours: 2);

        var (token, _) = sut.Create();
        clock.Advance(TimeSpan.FromHours(2).Add(TimeSpan.FromSeconds(1)));

        sut.Validate(token).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-token")]
    [InlineData("a.b.c")]
    [InlineData("!!!.@@@")]
    public void Validate_WhenMalformed_ShouldReturnFalse(string? invalidToken)
    {
        var clock = new FakeClock();
        var sut = CreateSut(clock);

        sut.Validate(invalidToken).Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenSignatureTampered_ShouldReturnFalse()
    {
        var clock = new FakeClock();
        var sut = CreateSut(clock);

        var (token, _) = sut.Create();
        var tampered = token[..^2] + (token.EndsWith("AA", StringComparison.Ordinal) ? "BB" : "AA");

        sut.Validate(tampered).Should().BeFalse();
    }
}
