using Microsoft.EntityFrameworkCore;
using SesliKitap.Data.Context;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Repositories;

/// <summary>
/// <see cref="IVoiceProfileRepository"/> sözleşmesinin EF Core uygulaması.
/// </summary>
public sealed class VoiceProfileRepository : GenericRepository<VoiceProfile>, IVoiceProfileRepository
{
    /// <summary>
    /// <see cref="VoiceProfileRepository"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public VoiceProfileRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VoiceProfile>> GetAllOrderedAsync(CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .OrderByDescending(x => x.VarsayilanMi)
            .ThenByDescending(x => x.OlusturmaTarihi)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByNameAsync(string ad, Guid? excludeId = null, CancellationToken ct = default)
    {
        var trimmed = ad.Trim();
        var query = DbSet.AsNoTracking();
        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        if (Context.Database.IsNpgsql())
        {
            return await query.AnyAsync(x => EF.Functions.ILike(x.Ad, trimmed), ct);
        }

        return await query.AnyAsync(
            x => string.Equals(x.Ad, trimmed, StringComparison.OrdinalIgnoreCase),
            ct);
    }

    /// <inheritdoc />
    public async Task SetDefaultAsync(Guid id, CancellationToken ct = default)
    {
        var strategy = Context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = Context.Database.IsRelational()
                ? await Context.Database.BeginTransactionAsync(ct)
                : null;

            var all = await DbSet.Where(x => x.VarsayilanMi || x.Id == id).ToListAsync(ct);
            foreach (var item in all.Where(x => x.Id != id && x.VarsayilanMi))
            {
                item.VarsayilanMi = false;
            }

            await Context.SaveChangesAsync(ct);

            var target = all.FirstOrDefault(x => x.Id == id);
            if (target is not null && !target.VarsayilanMi)
            {
                target.VarsayilanMi = true;
                await Context.SaveChangesAsync(ct);
            }

            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VoiceProfile>> GetExpiredSoftDeletedAsync(
        DateTime cutoffUtc,
        CancellationToken ct = default)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .Where(x => x.SilindiMi && x.SilinmeTarihi != null && x.SilinmeTarihi <= cutoffUtc)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task HardDeleteAsync(VoiceProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (Context.Database.IsRelational())
        {
            await DbSet
                .IgnoreQueryFilters()
                .Where(x => x.Id == profile.Id)
                .ExecuteDeleteAsync(ct);
            return;
        }

        profile.SilindiMi = true;
        DbSet.Remove(profile);
        await Context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<long> SumGpuMsForMonthAsync(int year, int month, CancellationToken ct = default)
    {
        var startUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = startUtc.AddMonths(1);

        return await DbSet
            .IgnoreQueryFilters()
            .Where(x => x.OlusturmaTarihi >= startUtc && x.OlusturmaTarihi < endUtc)
            .SumAsync(x => x.GpuMs, ct);
    }
}
