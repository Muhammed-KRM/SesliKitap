using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SesliKitap.Data.Entities.Base;

namespace SesliKitap.Data.Context;

/// <summary>
/// EF Core <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> çağrılarında
/// oluşturulma/güncellenme zamanlarını ve yumuşak silme (soft delete) dönüşümünü otomatik uygulayan kesici.
/// </summary>
public sealed class AuditingInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditingRules(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditingRules(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ApplyAuditingRules(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable softDeletable })
            {
                entry.State = EntityState.Modified;
                softDeletable.SilindiMi = true;
                softDeletable.SilinmeTarihi = utcNow;
            }

            if (entry.Entity is AuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    if (auditable.OlusturmaTarihi == default)
                    {
                        auditable.OlusturmaTarihi = utcNow;
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.GuncellemeTarihi = utcNow;
                }
            }
        }
    }
}
