using System.Text;
using FluentAssertions;
using SesliKitap.Business.Helpers;

namespace SesliKitap.UnitTests.Helpers;

public sealed class HashHelperTests
{
    [Fact]
    public async Task ComputeSha256Async_ShouldReturnDeterministicHexAndRewindStream()
    {
        var payload = Encoding.UTF8.GetBytes("Kürk Mantolu Madonna - Sabahattin Ali");
        using var stream = new MemoryStream(payload);
        stream.Position = 5;

        var hash = await HashHelper.ComputeSha256Async(stream);
        var stringHash = HashHelper.ComputeSha256("Kürk Mantolu Madonna - Sabahattin Ali");

        hash.Should().HaveLength(64);
        hash.Should().Be(stringHash);
        stream.Position.Should().Be(5);
    }

    [Fact]
    public async Task ComputeSha256Async_LargeStreamOver80KB_ShouldComputeCorrectHash()
    {
        var data = new byte[200_000];
        Random.Shared.NextBytes(data);
        using var stream = new MemoryStream(data);

        var streamHash = await HashHelper.ComputeSha256Async(stream);
        var spanHash = HashHelper.ComputeSha256(data.AsSpan());

        streamHash.Should().Be(spanHash);
    }

    [Fact]
    public void Sha1Hex_ShouldReturn40CharLowercaseHex()
    {
        var sha1 = HashHelper.Sha1Hex("sayfa=1&boyut=20&arama=madonna");

        sha1.Should().HaveLength(40);
        sha1.Should().MatchRegex("^[0-9a-f]{40}$");
    }
}
