using Microsoft.EntityFrameworkCore;
using SesliKitap.Data.Context;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Repositories;

/// <summary>
/// <see cref="IProgressRepository"/> arayüzünün Entity Framework Core ve PostgreSQL atomik upsert uygulaması.
/// </summary>
public sealed class ProgressRepository : GenericRepository<ListeningProgress>, IProgressRepository
{
    /// <summary>
    /// <see cref="ProgressRepository"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="context">Veritabanı bağlamı.</param>
    public ProgressRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<ListeningProgress?> GetByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(x => x.Chapter)
            .FirstOrDefaultAsync(x => x.BookId == bookId, ct);
    }

    /// <inheritdoc />
    public async Task<ListeningProgress> UpsertAsync(ListeningProgress progress, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        if (Context.Database.IsNpgsql())
        {
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO listening_progress (
                    id,
                    book_id,
                    chapter_id,
                    pozisyon_sn,
                    oynatma_hizi,
                    tamamlanma_yuzdesi,
                    cihaz_kimligi,
                    cihaz_adi,
                    istemci_zamani,
                    tamamlandi_mi,
                    olusturma_tarihi,
                    guncelleme_tarihi
                )
                VALUES (
                    {progress.Id},
                    {progress.BookId},
                    {progress.ChapterId},
                    {progress.PozisyonSn},
                    {progress.OynatmaHizi},
                    {progress.TamamlanmaYuzdesi},
                    {progress.CihazKimligi},
                    {progress.CihazAdi},
                    {progress.IstemciZamani},
                    {progress.TamamlandiMi},
                    {now},
                    {now}
                )
                ON CONFLICT (book_id) DO UPDATE SET
                    chapter_id = EXCLUDED.chapter_id,
                    pozisyon_sn = EXCLUDED.pozisyon_sn,
                    oynatma_hizi = EXCLUDED.oynatma_hizi,
                    tamamlanma_yuzdesi = EXCLUDED.tamamlanma_yuzdesi,
                    cihaz_kimligi = EXCLUDED.cihaz_kimligi,
                    cihaz_adi = EXCLUDED.cihaz_adi,
                    istemci_zamani = EXCLUDED.istemci_zamani,
                    tamamlandi_mi = EXCLUDED.tamamlandi_mi,
                    guncelleme_tarihi = EXCLUDED.guncelleme_tarihi
                WHERE listening_progress.istemci_zamani < EXCLUDED.istemci_zamani;", ct);

            await Context.Books
                .Where(b => b.Id == progress.BookId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.SonDinlenmeTarihi, now)
                    .SetProperty(b => b.GuncellemeTarihi, now), ct);

            var current = await GetByBookAsync(progress.BookId, ct);
            return current ?? progress;
        }

        // InMemory (birim testleri) geri dönüşü:
        var existing = await DbSet.FirstOrDefaultAsync(x => x.BookId == progress.BookId, ct);
        if (existing is null)
        {
            progress.OlusturmaTarihi = now;
            progress.GuncellemeTarihi = now;
            await DbSet.AddAsync(progress, ct);
        }
        else if (progress.IstemciZamani > existing.IstemciZamani)
        {
            existing.ChapterId = progress.ChapterId;
            existing.PozisyonSn = progress.PozisyonSn;
            existing.OynatmaHizi = progress.OynatmaHizi;
            existing.TamamlanmaYuzdesi = progress.TamamlanmaYuzdesi;
            existing.CihazKimligi = progress.CihazKimligi;
            existing.CihazAdi = progress.CihazAdi;
            existing.IstemciZamani = progress.IstemciZamani;
            existing.TamamlandiMi = progress.TamamlandiMi;
            existing.GuncellemeTarihi = now;
        }

        var book = await Context.Books.FirstOrDefaultAsync(b => b.Id == progress.BookId, ct);
        if (book is not null)
        {
            book.SonDinlenmeTarihi = now;
            book.GuncellemeTarihi = now;
        }

        await Context.SaveChangesAsync(ct);
        return (await GetByBookAsync(progress.BookId, ct)) ?? existing ?? progress;
    }
}
