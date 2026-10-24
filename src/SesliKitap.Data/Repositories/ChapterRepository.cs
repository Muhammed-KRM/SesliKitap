using Microsoft.EntityFrameworkCore;
using SesliKitap.Data.Context;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Repositories;

/// <summary>
/// <see cref="IChapterRepository"/> arayüzünün Entity Framework Core uygulaması.
/// </summary>
public sealed class ChapterRepository : GenericRepository<BookChapter>, IChapterRepository
{
    /// <summary>
    /// <see cref="ChapterRepository"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="context">Veritabanı bağlamı.</param>
    public ChapterRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChapterSummaryRow>> GetSummariesAsync(Guid bookId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(c => c.BookId == bookId)
            .OrderBy(c => c.SiraNo)
            .Select(c => new ChapterSummaryRow(
                c.Id,
                c.BookId,
                c.SiraNo,
                c.Baslik,
                c.KelimeSayisi,
                c.KarakterSayisi,
                c.DahilMi,
                c.OtomatikHaricNedeni,
                c.SesDurumu,
                c.SureSn,
                c.MetinSurumu))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BookChapter?> GetWithTextAsync(Guid id, bool asNoTracking = true, CancellationToken ct = default)
    {
        var query = asNoTracking ? DbSet.AsNoTracking() : DbSet;
        return await query.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookChapter>> GetByBookIdOrderedAsync(Guid bookId, bool asNoTracking = false, CancellationToken ct = default)
    {
        var query = asNoTracking ? DbSet.AsNoTracking() : DbSet;
        return await query
            .Where(c => c.BookId == bookId)
            .OrderBy(c => c.SiraNo)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteByBookIdAsync(Guid bookId, CancellationToken ct = default)
    {
        if (Context.Database.IsRelational())
        {
            await DbSet
                .Where(c => c.BookId == bookId)
                .ExecuteDeleteAsync(ct);
        }
        else
        {
            var existing = await DbSet.Where(c => c.BookId == bookId).ToListAsync(ct);
            if (existing.Count > 0)
            {
                DbSet.RemoveRange(existing);
                await Context.SaveChangesAsync(ct);
            }
        }
    }

    /// <inheritdoc />
    public async Task ShiftOrderAsync(Guid bookId, int fromSiraNo, int delta, CancellationToken ct = default)
    {
        if (delta == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (Context.Database.IsRelational())
        {
            // Benzersiz (book_id, sira_no) indeks çakışmasını önlemek için önce negatif geçici sıra numarasına al
            await DbSet
                .Where(c => c.BookId == bookId && c.SiraNo >= fromSiraNo)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.SiraNo, c => -(c.SiraNo + delta))
                    .SetProperty(c => c.GuncellemeTarihi, now), ct);

            await DbSet
                .Where(c => c.BookId == bookId && c.SiraNo < 0)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.SiraNo, c => -c.SiraNo), ct);
        }
        else
        {
            var items = await DbSet
                .Where(c => c.BookId == bookId && c.SiraNo >= fromSiraNo)
                .ToListAsync(ct);

            foreach (var item in items)
            {
                item.SiraNo += delta;
                item.GuncellemeTarihi = now;
            }

            await Context.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<int> RecalculateBookWordCountAsync(Guid bookId, CancellationToken ct = default)
    {
        var totalWords = await DbSet
            .AsNoTracking()
            .Where(c => c.BookId == bookId && c.DahilMi)
            .SumAsync(c => c.KelimeSayisi, ct);

        var now = DateTime.UtcNow;
        if (Context.Database.IsRelational())
        {
            await Context.Set<Book>()
                .Where(b => b.Id == bookId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.KelimeSayisi, totalWords)
                    .SetProperty(b => b.GuncellemeTarihi, now), ct);
        }
        else
        {
            var book = await Context.Set<Book>().FirstOrDefaultAsync(b => b.Id == bookId, ct);
            if (book is not null)
            {
                book.KelimeSayisi = totalWords;
                book.GuncellemeTarihi = now;
                await Context.SaveChangesAsync(ct);
            }
        }

        return totalWords;
    }

    /// <inheritdoc />
    public async Task UpdateInclusionBulkAsync(
        Guid bookId,
        IReadOnlyCollection<Guid> chapterIds,
        bool dahilMi,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (Context.Database.IsRelational())
        {
            await DbSet
                .Where(c => c.BookId == bookId && chapterIds.Contains(c.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.DahilMi, dahilMi)
                    .SetProperty(c => c.OtomatikHaricNedeni, c => dahilMi ? null : c.OtomatikHaricNedeni)
                    .SetProperty(c => c.GuncellemeTarihi, now), ct);
        }
        else
        {
            var chapters = await DbSet
                .Where(c => c.BookId == bookId && chapterIds.Contains(c.Id))
                .ToListAsync(ct);

            foreach (var chapter in chapters)
            {
                chapter.DahilMi = dahilMi;
                if (dahilMi)
                {
                    chapter.OtomatikHaricNedeni = null;
                }

                chapter.GuncellemeTarihi = now;
            }

            await Context.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookChapter>> GetForSynthesisAsync(
        Guid bookId,
        IReadOnlyList<Guid>? chapterIds,
        bool onlyMissing,
        CancellationToken ct = default)
    {
        var query = DbSet.Where(c => c.BookId == bookId);

        if (chapterIds is { Count: > 0 })
        {
            query = query.Where(c => chapterIds.Contains(c.Id));
        }
        else
        {
            query = query.Where(c => c.DahilMi);
        }

        if (onlyMissing)
        {
            query = query.Where(c =>
                c.SesDurumu != Data.Enums.BolumSesDurumu.Hazir ||
                c.SesMetinSurumu != c.MetinSurumu);
        }

        return await query
            .OrderBy(c => c.SiraNo)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<double> RecalculateBookAudioDurationAsync(Guid bookId, CancellationToken ct = default)
    {
        var totalDuration = await DbSet
            .AsNoTracking()
            .Where(c => c.BookId == bookId &&
                        c.DahilMi &&
                        (c.SesDurumu == Data.Enums.BolumSesDurumu.Hazir ||
                         c.SesDurumu == Data.Enums.BolumSesDurumu.GuncelDegil) &&
                        c.SureSn != null)
            .SumAsync(c => c.SureSn ?? 0d, ct);

        totalDuration = Math.Round(totalDuration, 2);
        var now = DateTime.UtcNow;

        if (Context.Database.IsRelational())
        {
            await Context.Set<Book>()
                .Where(b => b.Id == bookId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.ToplamSureSn, totalDuration)
                    .SetProperty(b => b.GuncellemeTarihi, now), ct);
        }
        else
        {
            var book = await Context.Set<Book>().FirstOrDefaultAsync(b => b.Id == bookId, ct);
            if (book is not null)
            {
                book.ToplamSureSn = totalDuration;
                book.GuncellemeTarihi = now;
                await Context.SaveChangesAsync(ct);
            }
        }

        return totalDuration;
    }
}
