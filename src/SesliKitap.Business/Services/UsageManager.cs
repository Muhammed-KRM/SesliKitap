using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Usage;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Interfaces;
using SesliKitap.Business.Options;
using SesliKitap.Data.Context;

namespace SesliKitap.Business.Services;

/// <summary>
/// Aylık GPU kullanım süresi, maliyet, üretilen ses saati ve son 6 aylık geçmişi raporlayan servis (Faz 8).
/// </summary>
public sealed class UsageManager : IUsageService
{
    private static readonly CultureInfo TrCulture = new("tr-TR");

    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly TtsOptions _ttsOptions;

    /// <summary>
    /// <see cref="UsageManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public UsageManager(
        AppDbContext db,
        IClock clock,
        IOptions<TtsOptions> ttsOptions)
    {
        _db = db;
        _clock = clock;
        _ttsOptions = ttsOptions.Value;
    }

    /// <inheritdoc />
    public async Task<MonthlyUsageDto> GetMonthlyUsageAsync(
        int? year = null,
        int? month = null,
        CancellationToken ct = default)
    {
        var nowUtc = _clock.UtcNow;
        var targetYear = year ?? nowUtc.Year;
        var targetMonth = month ?? nowUtc.Month;

        if (targetYear is < 2020 or > 2100 || targetMonth is < 1 or > 12)
        {
            throw new BusinessException("Geçersiz yıl veya ay değeri.", ErrorCodes.ValidationFailed);
        }

        var selectedMonthEnd = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        var sixMonthsStart = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);

        var jobsInWindow = await _db.SynthesisJobs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(j => j.OlusturmaTarihi >= sixMonthsStart && j.OlusturmaTarihi < selectedMonthEnd)
            .Select(j => new
            {
                j.Id,
                j.BookId,
                j.OlusturmaTarihi,
                j.GpuMs,
                j.TahminiMaliyetUsd,
                j.UretilenSesSn,
                j.OnbellektenParca,
                j.UretilenParca
            })
            .ToListAsync(ct);

        var currentMonthStart = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentMonthJobs = jobsInWindow
            .Where(j => j.OlusturmaTarihi >= currentMonthStart && j.OlusturmaTarihi < selectedMonthEnd)
            .ToList();

        var totalGpuMs = currentMonthJobs.Sum(j => j.GpuMs);
        var totalGpuHours = Math.Round(totalGpuMs / 3_600_000.0, 3);
        var totalCostUsd = Math.Round(currentMonthJobs.Sum(j => j.TahminiMaliyetUsd), 4);
        var monthlyBudgetUsd = _ttsOptions.MonthlyBudgetUsd;
        var budgetPercent = monthlyBudgetUsd > 0m
            ? Math.Round((double)(totalCostUsd / monthlyBudgetUsd) * 100.0, 1)
            : 0.0;

        var totalAudioSec = Math.Round(currentMonthJobs.Sum(j => j.UretilenSesSn), 1);
        var totalAudioHours = Math.Round(totalAudioSec / 3600.0, 2);
        var completedJobsCount = currentMonthJobs.Count;
        var distinctBooksCount = currentMonthJobs.Select(j => j.BookId).Distinct().Count();
        var cachedChunks = currentMonthJobs.Sum(j => (long)j.OnbellektenParca);
        var generatedChunks = currentMonthJobs.Sum(j => (long)j.UretilenParca);

        var history = new List<MonthlyUsageHistoryItemDto>(6);
        for (var i = 5; i >= 0; i--)
        {
            var mStart = currentMonthStart.AddMonths(-i);
            var mEnd = mStart.AddMonths(1);
            var mJobs = jobsInWindow
                .Where(j => j.OlusturmaTarihi >= mStart && j.OlusturmaTarihi < mEnd)
                .ToList();

            var mGpuMs = mJobs.Sum(j => j.GpuMs);
            var mCost = Math.Round(mJobs.Sum(j => j.TahminiMaliyetUsd), 4);
            var mAudioHours = Math.Round(mJobs.Sum(j => j.UretilenSesSn) / 3600.0, 2);
            var label = mStart.ToString("MMM yyyy", TrCulture);

            history.Add(new MonthlyUsageHistoryItemDto(
                mStart.Year,
                mStart.Month,
                label,
                mGpuMs,
                mCost,
                mAudioHours,
                mJobs.Count));
        }

        return new MonthlyUsageDto(
            targetYear,
            targetMonth,
            totalGpuMs,
            totalGpuHours,
            totalCostUsd,
            monthlyBudgetUsd,
            budgetPercent,
            totalAudioSec,
            totalAudioHours,
            completedJobsCount,
            distinctBooksCount,
            cachedChunks,
            generatedChunks,
            history);
    }
}
