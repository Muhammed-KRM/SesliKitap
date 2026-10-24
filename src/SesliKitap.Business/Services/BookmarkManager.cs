using Microsoft.EntityFrameworkCore;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Bookmark;
using SesliKitap.Business.Exceptions;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Context;
using SesliKitap.Data.Entities;

namespace SesliKitap.Business.Services;

/// <summary>
/// Kitap yer imlerini yöneten iş servisi (Faz 8).
/// </summary>
public sealed class BookmarkManager : IBookmarkService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// <see cref="BookmarkManager"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public BookmarkManager(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookmarkDto>> GetByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        var bookExists = await _db.Books.AsNoTracking().AnyAsync(b => b.Id == bookId, ct);
        if (!bookExists)
        {
            throw new NotFoundException("Kitap", bookId);
        }

        return await _db.Bookmarks
            .AsNoTracking()
            .Where(b => b.BookId == bookId)
            .OrderBy(b => b.Chapter != null ? b.Chapter.SiraNo : int.MaxValue)
            .ThenBy(b => b.PozisyonSn)
            .Select(b => new BookmarkDto(
                b.Id,
                b.BookId,
                b.ChapterId,
                b.Chapter != null ? b.Chapter.SiraNo : 0,
                b.Chapter != null ? b.Chapter.Baslik : string.Empty,
                b.PozisyonSn,
                b.Not,
                b.OlusturmaTarihi))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BookmarkDto> CreateAsync(
        Guid bookId,
        BookmarkCreateRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PozisyonSn < 0 || double.IsNaN(request.PozisyonSn) || double.IsInfinity(request.PozisyonSn))
        {
            throw new BusinessException("Yer imi konumu 0 veya daha büyük geçerli bir saniye olmalıdır.", ErrorCodes.ValidationFailed);
        }

        if (request.Not is not null && request.Not.Length > 1000)
        {
            throw new BusinessException("Yer imi notu en fazla 1000 karakter olabilir.", ErrorCodes.ValidationFailed);
        }

        var chapter = await _db.BookChapters
            .AsNoTracking()
            .Where(c => c.Id == request.ChapterId && c.BookId == bookId)
            .Select(c => new { c.Id, c.SiraNo, c.Baslik, c.SureSn })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Kitaba ait bölüm", request.ChapterId);

        if (chapter.SureSn.HasValue && request.PozisyonSn > chapter.SureSn.Value + 5.0)
        {
            throw new BusinessException("Yer imi konumu bölüm süresini aşamaz.", ErrorCodes.ValidationFailed);
        }

        var entity = new Bookmark
        {
            Id = Guid.CreateVersion7(),
            BookId = bookId,
            ChapterId = request.ChapterId,
            PozisyonSn = Math.Round(request.PozisyonSn, 2),
            Not = string.IsNullOrWhiteSpace(request.Not) ? null : request.Not.Trim()
        };

        await _db.Bookmarks.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        return new BookmarkDto(
            entity.Id,
            entity.BookId,
            entity.ChapterId,
            chapter.SiraNo,
            chapter.Baslik,
            entity.PozisyonSn,
            entity.Not,
            entity.OlusturmaTarihi);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Bookmarks.FirstOrDefaultAsync(b => b.Id == id, ct)
                     ?? throw new NotFoundException("Yer imi", id);

        _db.Bookmarks.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
