using FluentAssertions;
using Microsoft.Extensions.Options;
using SesliKitap.Business.Helpers;
using SesliKitap.Business.Options;
using SesliKitap.Business.Services;

namespace SesliKitap.UnitTests.Services;

public sealed class CostEstimatorTests
{
    private static CostEstimator CreateSut(TtsOptions? custom = null)
    {
        var options = custom ?? new TtsOptions
        {
            WordsPerMinute = 140,
            EstimatedRtf = 0.25,
            ColdStartSec = 60,
            IdleOverheadFactor = 1.15,
            GpuPricePerHourUsd = 0.59m
        };

        return new CostEstimator(Options.Create(options));
    }

    [Fact]
    public void DurationEstimator_50000Words_ReturnsApproximately21428SecondsAudio()
    {
        var audioSec = DurationEstimator.EstimateAudioSeconds(50_000, wordsPerMinute: 140);

        audioSec.Should().BeApproximately(21_428.6, 0.2);
    }

    [Fact]
    public void Estimate_50000Words_ComputesExpectedAudioGpuAndUsdCost()
    {
        var sut = CreateSut();

        var (audioSec, gpuSec, costUsd) = sut.Estimate(50_000, includeColdStart: true);

        // 50,000 / 140 * 60 = 21,428.6 sn ses
        audioSec.Should().BeApproximately(21_428.6, 0.2);
        // (21,428.6 * 0.25 + 60) * 1.15 = (5357.15 + 60) * 1.15 = 6229.7 sn GPU
        gpuSec.Should().BeApproximately(6_229.7, 1.0);
        // 6229.7 / 3600 * 0.59 = ~1.0210 USD
        costUsd.Should().BeApproximately(1.0210m, 0.01m);
    }

    [Fact]
    public void Estimate_ZeroOrNegativeWords_ReturnsAllZeros()
    {
        var sut = CreateSut();

        sut.Estimate(0).Should().Be((0d, 0d, 0m));
        sut.Estimate(-250).Should().Be((0d, 0d, 0m));
    }

    [Fact]
    public void CostFromGpuMs_CalculatesCostWithIdleOverheadAndRoundsToFourDecimals()
    {
        var sut = CreateSut();

        // 3,600,000 ms = 1 saat aktif GPU -> * 1.15 overhead * 0.59 USD/saat = 0.6785 USD
        var cost = sut.CostFromGpuMs(3_600_000);

        cost.Should().Be(0.6785m);
        sut.CostFromGpuMs(0).Should().Be(0m);
        sut.CostFromGpuMs(-100).Should().Be(0m);
    }
}
