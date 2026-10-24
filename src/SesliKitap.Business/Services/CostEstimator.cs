using Microsoft.Extensions.Options;
using SesliKitap.Business.Helpers;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;

namespace SesliKitap.Business.Services;

/// <summary>
/// <see cref="ICostEstimator"/> sözleşmesinin <see cref="TtsOptions"/> tabanlı saf hesaplama uygulaması.
/// </summary>
public sealed class CostEstimator : ICostEstimator
{
    private readonly TtsOptions _options;

    /// <summary>
    /// <see cref="CostEstimator"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public CostEstimator(IOptions<TtsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public (double AudioSec, double GpuSec, decimal CostUsd) Estimate(int words, bool includeColdStart = true)
    {
        if (words <= 0)
        {
            return (0d, 0d, 0m);
        }

        var audioSec = DurationEstimator.EstimateAudioSeconds(words, _options.WordsPerMinute);
        var coldStart = includeColdStart ? _options.ColdStartSec : 0;
        var gpuSec = DurationEstimator.EstimateGpuSeconds(
            audioSec,
            _options.EstimatedRtf,
            coldStart,
            _options.IdleOverheadFactor);
        var costUsd = DurationEstimator.EstimateCostUsd(gpuSec, _options.GpuPricePerHourUsd);

        return (audioSec, gpuSec, costUsd);
    }

    /// <inheritdoc />
    public decimal CostFromGpuMs(long gpuMs)
    {
        if (gpuMs <= 0)
        {
            return 0m;
        }

        var billedGpuSec = gpuMs / 1000.0 * _options.IdleOverheadFactor;
        return DurationEstimator.EstimateCostUsd(billedGpuSec, _options.GpuPricePerHourUsd);
    }
}
