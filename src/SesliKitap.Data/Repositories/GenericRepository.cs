using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SesliKitap.Data.Context;

namespace SesliKitap.Data.Repositories;

/// <summary>
/// <see cref="IRepository{T}"/> arayüzünün Entity Framework Core tabanlı temel uygulaması.
/// </summary>
/// <typeparam name="T">Varlık tipi.</typeparam>
public class GenericRepository<T> : IRepository<T> where T : class
{
    /// <summary>Alt sınıfların erişebileceği EF Core veritabanı bağlamı.</summary>
    protected AppDbContext Context { get; }

    /// <summary>Alt sınıfların erişebileceği varlık tablosu (<see cref="DbSet{TEntity}"/>).</summary>
    protected DbSet<T> DbSet { get; }

    /// <summary>
    /// <see cref="GenericRepository{T}"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="context">Veritabanı bağlamı.</param>
    public GenericRepository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    /// <inheritdoc />
    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet.FindAsync([id], ct);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<T>> GetAllAsync(bool asNoTracking = true, CancellationToken ct = default)
    {
        var query = asNoTracking ? DbSet.AsNoTracking() : DbSet;
        return await query.ToListAsync(ct);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        bool asNoTracking = true,
        CancellationToken ct = default)
    {
        var query = asNoTracking ? DbSet.AsNoTracking() : DbSet;
        return await query.Where(predicate).ToListAsync(ct);
    }

    /// <inheritdoc />
    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking().AnyAsync(predicate, ct);
    }

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking().CountAsync(predicate, ct);
    }

    /// <inheritdoc />
    public virtual IQueryable<T> Query(bool asNoTracking = true)
    {
        return asNoTracking ? DbSet.AsNoTracking() : DbSet;
    }

    /// <inheritdoc />
    public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await DbSet.AddAsync(entity, ct);
        return entity;
    }

    /// <inheritdoc />
    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        await DbSet.AddRangeAsync(entities, ct);
    }

    /// <inheritdoc />
    public virtual void Update(T entity)
    {
        DbSet.Update(entity);
    }

    /// <inheritdoc />
    public virtual void Delete(T entity)
    {
        DbSet.Remove(entity);
    }

    /// <inheritdoc />
    public virtual async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await Context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public virtual async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!Context.Database.IsRelational())
        {
            await action(ct);
            return;
        }

        var strategy = Context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async token =>
        {
            await using var tx = await Context.Database.BeginTransactionAsync(token);
            await action(token);
            await tx.CommitAsync(token);
        }, ct);
    }
}
