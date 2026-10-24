using Microsoft.EntityFrameworkCore;
using SesliKitap.Data.Context;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;

namespace SesliKitap.Data.Repositories;

/// <summary>
/// <see cref="ISynthesisJobRepository"/> sözleşmesinin EF Core uygulaması.
/// </summary>
public sealed class SynthesisJobRepository : GenericRepository<SynthesisJob>, ISynthesisJobRepository
{
    /// <summary>
    /// <see cref="SynthesisJobRepository"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public SynthesisJobRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<SynthesisJob?> GetActiveJobForBookAsync(Guid bookId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(j => j.BookId == bookId &&
                        (j.Durum == SentezIsDurumu.Kuyrukta || j.Durum == SentezIsDurumu.Calisiyor))
            .OrderByDescending(j => j.OlusturmaTarihi)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> AnyActiveJobAsync(Guid bookId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .AnyAsync(
                j => j.BookId == bookId &&
                     (j.Durum == SentezIsDurumu.Kuyrukta || j.Durum == SentezIsDurumu.Calisiyor),
                ct);
    }

    /// <inheritdoc />
    public async Task<bool> AnyActiveJobForVoiceProfileAsync(Guid voiceProfileId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .AnyAsync(
                j => j.VoiceProfileId == voiceProfileId &&
                     (j.Durum == SentezIsDurumu.Kuyrukta || j.Durum == SentezIsDurumu.Calisiyor),
                ct);
    }

    /// <inheritdoc />
    public async Task<SynthesisJob?> GetWithDetailsAsync(
        Guid jobId,
        bool asNoTracking = false,
        CancellationToken ct = default)
    {
        IQueryable<SynthesisJob> query = DbSet
            .Include(j => j.Book)
            .Include(j => j.VoiceProfile)
            .Include(j => j.Bolumler)
                .ThenInclude(jc => jc.Chapter);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(j => j.Id == jobId, ct);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<SynthesisJob> Items, int TotalCount)> GetPagedListAsync(
        SentezIsDurumu? durum,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Include(j => j.Book)
            .Include(j => j.VoiceProfile)
            .AsQueryable();

        if (durum.HasValue)
        {
            query = query.Where(j => j.Durum == durum.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(j => j.OlusturmaTarihi)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<decimal> SumCostForMonthAsync(int year, int month, CancellationToken ct = default)
    {
        var startUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = startUtc.AddMonths(1);

        return await DbSet
            .IgnoreQueryFilters()
            .Where(j => j.OlusturmaTarihi >= startUtc && j.OlusturmaTarihi < endUtc)
            .SumAsync(j => j.TahminiMaliyetUsd, ct);
    }

    /// <inheritdoc />
    public async Task<long> SumGpuMsForMonthAsync(int year, int month, CancellationToken ct = default)
    {
        var startUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = startUtc.AddMonths(1);

        return await DbSet
            .IgnoreQueryFilters()
            .Where(j => j.OlusturmaTarihi >= startUtc && j.OlusturmaTarihi < endUtc)
            .SumAsync(j => j.GpuMs, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SynthesisJob>> GetStuckJobsAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        return await DbSet
            .Where(j => (j.Durum == SentezIsDurumu.Kuyrukta || j.Durum == SentezIsDurumu.Calisiyor) &&
                        ((j.SonIlerlemeZamani != null && j.SonIlerlemeZamani <= cutoffUtc) ||
                         (j.SonIlerlemeZamani == null && j.OlusturmaTarihi <= cutoffUtc)))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<SynthesisJobChapter?> GetJobChapterAsync(
        Guid jobId,
        Guid chapterId,
        CancellationToken ct = default)
    {
        return await Context.Set<SynthesisJobChapter>()
            .FirstOrDefaultAsync(jc => jc.JobId == jobId && jc.ChapterId == chapterId, ct);
    }

    /// <inheritdoc />
    public async Task AddJobChaptersAsync(IEnumerable<SynthesisJobChapter> items, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        await Context.Set<SynthesisJobChapter>().AddRangeAsync(items, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetCompletedJobIdsBeforeAsync(
        DateTime cutoffUtc,
        CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(j => j.Durum != SentezIsDurumu.Kuyrukta &&
                        j.Durum != SentezIsDurumu.Calisiyor &&
                        j.OlusturmaTarihi <= cutoffUtc)
            .Select(j => j.Id)
            .ToListAsync(ct);
    }
}
