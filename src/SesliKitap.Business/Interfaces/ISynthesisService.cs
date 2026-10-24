using SesliKitap.Business.DTOs.Common;
using SesliKitap.Business.DTOs.Synthesis;
using SesliKitap.Business.Events;
using SesliKitap.Data.Enums;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Kitap bölümlerinin seslendirilmesi (sentez) iş akışını yöneten servis sözleşmesi (Bölüm 4.15.4).
/// </summary>
public interface ISynthesisService
{
    /// <summary>
    /// Sentez işi başlatılmadan önce tahmini ses süresi, GPU süresi, maliyet ve aylık bütçe durumunu hesaplar.
    /// </summary>
    Task<SynthesisEstimateDto> EstimateAsync(
        Guid bookId,
        Guid voiceProfileId,
        IReadOnlyList<Guid>? chapterIds,
        bool sadeceEksikler = true,
        CancellationToken ct = default);

    /// <summary>
    /// Kitap için yeni bir sentez işi başlatır, girdi JSON'unu MinIO'ya yükler ve RabbitMQ olayını yayınlar.
    /// </summary>
    Task<SynthesisJobDto> StartAsync(
        Guid bookId,
        SynthesisStartRequest request,
        string traceId,
        CancellationToken ct = default);

    /// <summary>
    /// Devam eden veya kuyruktaki bir sentez işini iptal eder.
    /// </summary>
    Task CancelAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Hatalı, iptal edilmiş veya kısmen tamamlanmış bir işi yalnızca eksik bölümlerle yeniden başlatır.
    /// </summary>
    Task<SynthesisJobDto> RetryAsync(Guid jobId, string traceId, CancellationToken ct = default);

    /// <summary>
    /// Sentez işlerini durum filtresi ve sayfalamayla listeler.
    /// </summary>
    Task<PagedResult<SynthesisJobDto>> GetListAsync(
        SentezIsDurumu? durum,
        PageQuery page,
        CancellationToken ct = default);

    /// <summary>
    /// Sentez işi detayını bölüm durumlarıyla birlikte getirir.
    /// </summary>
    Task<SynthesisJobDetailDto> GetByIdAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Worker'dan gelen sentez aşama/ilerleme olayını işler.
    /// </summary>
    Task HandleStageChangedAsync(PipelineStageChangedEvent stageEvent, CancellationToken ct = default);

    /// <summary>
    /// Worker'dan gelen bölüm sesi hazır olayını idempotent olarak işler.
    /// </summary>
    Task HandleChapterSynthesizedAsync(ChapterSynthesizedEvent chapterEvent, CancellationToken ct = default);

    /// <summary>
    /// Worker'dan gelen sentez işi tamamlandı olayını işler.
    /// </summary>
    Task HandleCompletedAsync(SynthesisCompletedEvent completedEvent, CancellationToken ct = default);

    /// <summary>
    /// Worker'dan gelen sentez hata olayını işler.
    /// </summary>
    Task HandleErrorAsync(PipelineErrorEvent errorEvent, CancellationToken ct = default);

    /// <summary>
    /// Watchdog tarafından tespit edilen takılı işi hatalı olarak işaretler ve kilidini serbest bırakır.
    /// </summary>
    Task MarkJobFailedAsync(Guid jobId, string reason, CancellationToken ct = default);
}
