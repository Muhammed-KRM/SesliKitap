namespace SesliKitap.Business.DTOs.Health;

/// <summary>
/// Tek bir altyapı bileşeninin (PostgreSQL, Redis, RabbitMQ, MinIO, TTS) sağlık durumu.
/// </summary>
/// <param name="Status">Bileşen durumu (<c>Healthy</c>, <c>Degraded</c>, <c>Unhealthy</c>).</param>
/// <param name="DurationMs">Kontrol süresi (milisaniye).</param>
/// <param name="Description">Varsa açıklama veya hata özeti.</param>
public sealed record HealthComponentDto(
    string Status,
    long DurationMs,
    string? Description = null);

/// <summary>
/// Kimliği doğrulanmış <c>GET /api/health</c> çağrılarında dönen ayrıntılı sistem sağlık raporu.
/// </summary>
/// <param name="Status">Genel sistem durumu (<c>Healthy</c>, <c>Degraded</c>, <c>Unhealthy</c>).</param>
/// <param name="Version">Uygulama sürümü.</param>
/// <param name="TotalDurationMs">Toplam kontrol süresi (milisaniye).</param>
/// <param name="TimestampUtc">Raporun üretildiği UTC zamanı.</param>
/// <param name="Components">Bileşen bazlı sağlık durumları.</param>
public sealed record HealthReportDto(
    string Status,
    string Version,
    long TotalDurationMs,
    DateTimeOffset TimestampUtc,
    IReadOnlyDictionary<string, HealthComponentDto> Components);
