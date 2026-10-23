using FluentAssertions;
using SesliKitap.Business.Helpers;

namespace SesliKitap.UnitTests.Helpers;

public sealed class DurationEstimatorTests
{
    [Fact]
    public void EstimateAudioSeconds_50000Words_ReturnsApproximately21428Seconds()
    {
        var seconds = DurationEstimator.EstimateAudioSeconds(50_000, 140);
        seconds.Should().BeApproximately(21_428.6, 0.1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void EstimateAudioSeconds_NonPositiveWords_ReturnsZero(int words)
    {
        DurationEstimator.EstimateAudioSeconds(words).Should().Be(0d);
    }

    [Fact]
    public void EstimateGpuSeconds_AndCostUsd_ComputesExpectedValues()
    {
        var audioSec = DurationEstimator.EstimateAudioSeconds(50_000, 140);
        var gpuSec = DurationEstimator.EstimateGpuSeconds(audioSec, rtf: 0.20, coldStartSeconds: 90, idleOverheadFactor: 1.15);
        gpuSec.Should().BeGreaterThan(4900);

        var cost = DurationEstimator.EstimateCostUsd(gpuSec, pricePerHourUsd: 0.80m);
        cost.Should().BeGreaterThan(1.0m).And.BeLessThan(1.3m);
    }
}
