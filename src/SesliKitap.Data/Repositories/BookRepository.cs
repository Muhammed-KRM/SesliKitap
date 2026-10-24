using Microsoft.EntityFrameworkCore;
using SesliKitap.Data.Context;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;

namespace SesliKitap.Data.Repositories;

/// <summary>
/// <see cref="IBookRepository"/> arayüzünün Entity Framework Core uygulaması.
/// </summary>
public sealed class BookRepository : GenericRepository<Book>, IBookRepository
{
    /// <summary>
    /// <see cref="BookRepository"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="context">Veritabanı bağlamı.</param>
    public BookRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<Book?> FindByHashAsync(string hash, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.DosyaHash == hash, ct);
    }

    /// <inheritdoc />
    public async Task<Book?> FindBySourceAsync(KaynakTipi kaynakTipi, string kaynakKimligi, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.KaynakTipi == kaynakTipi && b.KaynakKimligi == kaynakKimligi, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<(KaynakTipi Tip, string Kimlik), Guid>> GetExistingSourceMapAsync(CancellationToken ct = default)
    {
        var rows = await DbSet
            .AsNoTracking()
            .Where(b => b.KaynakKimligi != null && b.KaynakTipi != KaynakTipi.Yukleme)
            .Select(b => new { b.Id, b.KaynakTipi, b.KaynakKimligi })
            .ToListAsync(ct);

        var dict = new Dictionary<(KaynakTipi, string), Guid>();
        foreach (var r in rows)
        {
            if (!string.IsNullOrWhiteSpace(r.KaynakKimligi))
            {
                dict[(r.KaynakTipi, r.KaynakKimligi)] = r.Id;
            }
        }

        return dict;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<BookListRow> Items, int Toplam)> GetPagedListAsync(
        string? arama,
        KitapDurumu? durum,
        string? siralama,
        int sayfa,
        int boyut,
        CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var term = arama.Trim();
            if (Context.Database.IsNpgsql())
            {
                var pattern = $"%{term}%";
                query = query.Where(b =>
                    EF.Functions.ILike(b.Baslik, pattern) ||
                    (b.Yazar != null && EF.Functions.ILike(b.Yazar, pattern)));
            }
            else
            {
                query = query.Where(b =>
                    b.Baslik.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (b.Yazar != null && b.Yazar.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }
        }

        if (durum.HasValue)
        {
            query = query.Where(b => b.Durum == durum.Value);
        }

        var toplam = await query.CountAsync(ct);

        query = (siralama?.ToLowerInvariant()) switch
        {
            "sondinlenen" => query
                .OrderByDescending(b => b.SonDinlenmeTarihi.HasValue)
                .ThenByDescending(b => b.SonDinlenmeTarihi)
                .ThenByDescending(b => b.OlusturmaTarihi),
            "baslik" => query.OrderBy(b => b.Baslik),
            _ => query.OrderByDescending(b => b.OlusturmaTarihi)
        };

        var skip = Math.Max(0, (sayfa - 1) * boyut);
        var items = await query
            .Skip(skip)
            .Take(boyut)
            .Select(b => new BookListRow(
                b.Id,
                b.Baslik,
                b.Yazar,
                b.Durum,
                b.Bolumler.Count(c => c.DahilMi),
                b.Bolumler.Count(c => c.DahilMi && c.SesDurumu == BolumSesDurumu.Hazir),
                b.ToplamSureSn,
                b.Konum != null ? (double?)b.Konum.TamamlanmaYuzdesi : null,
                b.KapakYolu != null,
                b.OlusturmaTarihi,
                b.SonDinlenmeTarihi))
            .ToListAsync(ct);

        return (items, toplam);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookListRow>> GetContinueListeningAsync(int count, CancellationToken ct = default)
    {
        var safeCount = Math.Clamp(count, 1, 50);

        return await DbSet
            .AsNoTracking()
            .Where(b => b.SonDinlenmeTarihi != null && b.Konum != null && !b.Konum.TamamlandiMi)
            .OrderByDescending(b => b.SonDinlenmeTarihi)
            .Take(safeCount)
            .Select(b => new BookListRow(
                b.Id,
                b.Baslik,
                b.Yazar,
                b.Durum,
                b.Bolumler.Count(c => c.DahilMi),
                b.Bolumler.Count(c => c.DahilMi && c.SesDurumu == BolumSesDurumu.Hazir),
                b.ToplamSureSn,
                b.Konum != null ? (double?)b.Konum.TamamlanmaYuzdesi : null,
                b.KapakYolu != null,
                b.OlusturmaTarihi,
                b.SonDinlenmeTarihi))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateStatusAsync(Guid id, KitapDurumu durum, string? hataMesaji = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (Context.Database.IsRelational())
        {
            await DbSet
                .Where(b => b.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.Durum, durum)
                    .SetProperty(b => b.HataMesaji, hataMesaji)
                    .SetProperty(b => b.GuncellemeTarihi, now), ct);
        }
        else
        {
            var book = await DbSet.FirstOrDefaultAsync(b => b.Id == id, ct);
            if (book is not null)
            {
                book.Durum = durum;
                book.HataMesaji = hataMesaji;
                book.GuncellemeTarihi = now;
                await Context.SaveChangesAsync(ct);
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Book>> GetExpiredSoftDeletedBooksAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(b => b.SilindiMi && b.SilinmeTarihi != null && b.SilinmeTarihi < cutoffUtc)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task HardDeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (Context.Database.IsRelational())
        {
            await DbSet
                .IgnoreQueryFilters()
                .Where(b => b.Id == id)
                .ExecuteDeleteAsync(ct);
        }
        else
        {
            var entity = await DbSet.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id, ct);
            if (entity is not null)
            {
                entity.SilindiMi = true;
                DbSet.Remove(entity);
                await Context.SaveChangesAsync(ct);
            }
        }
    }
}
