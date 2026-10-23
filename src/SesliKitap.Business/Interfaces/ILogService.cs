using System.Runtime.CompilerServices;
using SesliKitap.Business.DTOs.Logs;
using SesliKitap.Data.Entities;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// 3 katmanlı (EndpointLog, FunctionLog, PipelineLog) hata-güvenli (fail-safe) veritabanı loglama servisi sözleşmesi.
/// </summary>
public interface ILogService
{
    /// <summary>
    /// HTTP uç noktası istek/yanıt bilgisini <c>endpoint_logs</c> tablosuna yazar.
    /// </summary>
    /// <param name="log">Endpoint log kaydı.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task LogEndpointAsync(EndpointLog log, CancellationToken ct = default);

    /// <summary>
    /// Bir pipeline aşamasının başlangıcını <c>pipeline_logs</c> tablosuna yazar ve üretilen log kimliğini döner.
    /// </summary>
    /// <param name="ctx">Pipeline bağlam bilgisi (kitap, iş, bölüm, izleme kimliği).</param>
    /// <param name="asama">Aşama adı.</param>
    /// <param name="girdiMetadata">İsteğe bağlı girdi meta verisi (JSON veya metin).</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Oluşturulan <c>pipeline_logs.id</c> değeri (hata durumunda <c>0</c>).</returns>
    Task<long> LogPipelineStartAsync(
        PipelineLogContext ctx,
        string asama,
        string? girdiMetadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Başlatılmış bir pipeline aşamasını <c>Tamamlandi</c> olarak günceller ve süreyi (<c>sure_ms</c>) hesaplar.
    /// </summary>
    /// <param name="logId"><see cref="LogPipelineStartAsync"/> tarafından dönen log kimliği.</param>
    /// <param name="ciktiMetadata">İsteğe bağlı çıktı meta verisi.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task LogPipelineEndAsync(long logId, string? ciktiMetadata = null, CancellationToken ct = default);

    /// <summary>
    /// Başlatılmış bir pipeline aşamasını <c>Hata</c> olarak günceller.
    /// </summary>
    /// <param name="logId"><see cref="LogPipelineStartAsync"/> tarafından dönen log kimliği.</param>
    /// <param name="ex">Oluşan istisna.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task LogPipelineErrorAsync(long logId, Exception ex, CancellationToken ct = default);

    /// <summary>
    /// Python Worker veya dış bileşenlerden gelen pipeline ilerleme/durum kaydını akıllı filtrelemeyle yazar.
    /// </summary>
    /// <param name="request">Pipeline log isteği.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task LogPipelineEventAsync(PipelineLogRequest request, CancellationToken ct = default);

    /// <summary>
    /// Fonksiyon veya servis seviyesinde oluşan bir istisnayı çağıran metot/dosya/satır bilgileriyle <c>function_logs</c> tablosuna yazar.
    /// </summary>
    /// <param name="errorCode">Standart hata kodu (<see cref="Constants.ErrorCodes"/>).</param>
    /// <param name="ex">Yakalanan istisna.</param>
    /// <param name="input">Metoda geçilen girdi nesnesi (varsa maskelenerek serileştirilir).</param>
    /// <param name="traceId">İzleme kimliği.</param>
    /// <param name="methodName">Derleyici tarafından otomatik doldurulan metot adı.</param>
    /// <param name="filePath">Derleyici tarafından otomatik doldurulan kaynak dosya yolu.</param>
    /// <param name="lineNumber">Derleyici tarafından otomatik doldurulan satır numarası.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task LogFunctionErrorAsync(
        string errorCode,
        Exception ex,
        object? input = null,
        string? traceId = null,
        [CallerMemberName] string methodName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        CancellationToken ct = default);

    /// <summary>
    /// Fonksiyon veya servis seviyesinde oluşan bir uyarıyı <c>function_logs</c> tablosuna (<c>Severity="Warning"</c>) yazar.
    /// </summary>
    /// <param name="code">Standart uyarı kodu.</param>
    /// <param name="message">Uyarı açıklaması.</param>
    /// <param name="input">Metoda geçilen girdi nesnesi.</param>
    /// <param name="traceId">İzleme kimliği.</param>
    /// <param name="methodName">Derleyici tarafından otomatik doldurulan metot adı.</param>
    /// <param name="filePath">Derleyici tarafından otomatik doldurulan kaynak dosya yolu.</param>
    /// <param name="lineNumber">Derleyici tarafından otomatik doldurulan satır numarası.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task LogFunctionWarningAsync(
        string code,
        string message,
        object? input = null,
        string? traceId = null,
        [CallerMemberName] string methodName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        CancellationToken ct = default);
}
