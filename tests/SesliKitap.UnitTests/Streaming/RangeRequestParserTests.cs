using FluentAssertions;
using Microsoft.Extensions.Primitives;
using SesliKitap.API.Streaming;

namespace SesliKitap.UnitTests.Streaming;

public sealed class RangeRequestParserTests
{
    private const long SampleTotalLength = 10_000;

    [Fact]
    public void Parse_OpenEndedFromZero_ReturnsFullSatisfiableRange()
    {
        var result = RangeRequestParser.Parse(new StringValues("bytes=0-"), SampleTotalLength);

        result.Should().NotBeNull();
        result!.IsSatisfiable.Should().BeTrue();
        result.Start.Should().Be(0);
        result.End.Should().Be(SampleTotalLength - 1);
        result.Length.Should().Be(SampleTotalLength);
    }

    [Fact]
    public void Parse_BoundedRange_ReturnsExactStartAndEnd()
    {
        var result = RangeRequestParser.Parse(new StringValues("bytes=100-199"), SampleTotalLength);

        result.Should().NotBeNull();
        result!.IsSatisfiable.Should().BeTrue();
        result.Start.Should().Be(100);
        result.End.Should().Be(199);
        result.Length.Should().Be(100);
    }

    [Fact]
    public void Parse_SingleByteRange_ReturnsLengthOne()
    {
        var result = RangeRequestParser.Parse(new StringValues("bytes=0-0"), SampleTotalLength);

        result.Should().NotBeNull();
        result!.IsSatisfiable.Should().BeTrue();
        result.Start.Should().Be(0);
        result.End.Should().Be(0);
        result.Length.Should().Be(1);
    }

    [Fact]
    public void Parse_SuffixRange_ReturnsLastNBytes()
    {
        var result = RangeRequestParser.Parse(new StringValues("bytes=-500"), SampleTotalLength);

        result.Should().NotBeNull();
        result!.IsSatisfiable.Should().BeTrue();
        result.Start.Should().Be(9_500);
        result.End.Should().Be(9_999);
        result.Length.Should().Be(500);
    }

    [Fact]
    public void Parse_SuffixRangeExceedingTotalLength_ClampsStartToZero()
    {
        var result = RangeRequestParser.Parse(new StringValues("bytes=-999999"), SampleTotalLength);

        result.Should().NotBeNull();
        result!.IsSatisfiable.Should().BeTrue();
        result.Start.Should().Be(0);
        result.End.Should().Be(SampleTotalLength - 1);
        result.Length.Should().Be(SampleTotalLength);
    }

    [Fact]
    public void Parse_EndExceedingTotalLength_ClampsEndToTotalLengthMinusOne()
    {
        var result = RangeRequestParser.Parse(new StringValues("bytes=500-999999"), SampleTotalLength);

        result.Should().NotBeNull();
        result!.IsSatisfiable.Should().BeTrue();
        result.Start.Should().Be(500);
        result.End.Should().Be(SampleTotalLength - 1);
        result.Length.Should().Be(9_500);
    }

    [Fact]
    public void Parse_StartEqualOrGreaterThanTotalLength_ReturnsUnsatisfiable()
    {
        var result = RangeRequestParser.Parse(new StringValues("bytes=10000-"), SampleTotalLength);

        result.Should().NotBeNull();
        result!.IsSatisfiable.Should().BeFalse();
        result.Length.Should().Be(0);
    }

    [Fact]
    public void Parse_ZeroSuffixRange_ReturnsUnsatisfiable()
    {
        var result = RangeRequestParser.Parse(new StringValues("bytes=-0"), SampleTotalLength);

        result.Should().NotBeNull();
        result!.IsSatisfiable.Should().BeFalse();
        result.Length.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("items=0-99")]
    [InlineData("bytes=")]
    [InlineData("bytes=-")]
    [InlineData("bytes=200-100")]
    [InlineData("bytes=0-99, 200-299")]
    [InlineData("bytes=abc-def")]
    [InlineData("bytes=100")]
    public void Parse_InvalidOrUnsupportedHeader_ReturnsNull(string rawHeader)
    {
        var result = RangeRequestParser.Parse(new StringValues(rawHeader), SampleTotalLength);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyStringValuesOrZeroTotalLength_ReturnsNull()
    {
        RangeRequestParser.Parse(StringValues.Empty, SampleTotalLength).Should().BeNull();
        RangeRequestParser.Parse(new StringValues("bytes=0-10"), 0).Should().BeNull();
    }
}
