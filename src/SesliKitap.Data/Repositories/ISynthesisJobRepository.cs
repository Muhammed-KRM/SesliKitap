using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;

namespace SesliKitap.Data.Repositories;

/// <summary>
/// Sentez işleri (<see cref="SynthesisJob"/> ve <see cref="SynthesisJobChapter"/>) için veri erişim sözleşmesi (Bölüm 4.15.5).
/// </summary>
public interface ISynthesisJobRepository : IRepository<SynthesisJob>
{
    /// <summary>
    /// Belirtilen kitap için devam eden (<see cref="SentezIsDurumu.Kuyrukta"/> veya <see cref="SentezIsDurumu.Calisiyor"/>) aktif işi getirir.
    /// </summary>
    Task<SynthesisJob?> GetActiveJobForBookAsync(Guid bookId, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen kitap için devam eden aktif bir sentez işi olup olmadığını kontrol eder.
    /// </summary>
    Task<bool> AnyActiveJobAsync(Guid bookId, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen ses profilini kullanan devam eden aktif bir sentez işi olup olmadığını kontrol eder.
    /// </summary>
    Task<bool> AnyActiveJobForVoiceProfileAsync(Guid voiceProfileId, CancellationToken ct = default);

    /// <summary>
    /// Sentez işini bölüm kayıtları, kitap ve ses profili bilgileriyle birlikte getirir.
    /// </summary>
    Task<SynthesisJob?> GetWithDetailsAsync(Guid jobId, bool asNoTracking = false, CancellationToken ct = default);

    /// <summary>
    /// Sentez işlerini durum filtresi ve sayfalamayla getirir.
    /// </summary>
    Task<(IReadOnlyList<SynthesisJob> Items, int TotalCount)> GetPagedListAsync(
        SentezIsDurumu? durum,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Belirtilen yıl ve ay içinde oluşturulan işlerin toplam USD maliyetini hesaplar.
    /// </summary>
    Task<decimal> SumCostForMonthAsync(int year, int month, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen yıl ve ay içinde oluşturulan işlerin toplam GPU süresini (ms) hesaplar.
    /// </summary>
    Task<long> SumGpuMsForMonthAsync(int year, int month, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen eşik süresinden beri ilerleme bildirmeyen (<see cref="SentezIsDurumu.Kuyrukta"/> veya <see cref="SentezIsDurumu.Calisiyor"/>) takılı işleri getirir.
    /// </summary>
    Task<IReadOnlyList<SynthesisJob>> GetStuckJobsAsync(DateTime cutoffUtc, CancellationToken ct = default);

    /// <summary>
    /// Tekil <see cref="SynthesisJobChapter"/> kaydını getirir.
    /// </summary>
    Task<SynthesisJobChapter?> GetJobChapterAsync(Guid jobId, Guid chapterId, CancellationToken ct = default);

    /// <summary>
    /// İşe ait bölüm kayıtlarını topluca ekler.
    /// </summary>
    Task AddJobChaptersAsync(IEnumerable<SynthesisJobChapter> items, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen tarihten önce tamamlanmış işlerin kimliklerini getirir (girdi JSON temizliği için).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetCompletedJobIdsBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default);
}
