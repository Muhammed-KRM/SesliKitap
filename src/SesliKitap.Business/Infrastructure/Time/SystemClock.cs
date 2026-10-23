using SesliKitap.Business.Interfaces;

namespace SesliKitap.Business.Infrastructure.Time;

/// <summary>
/// <see cref="TimeProvider"/> tabanlı üretim sistem saati uygulaması.
/// </summary>
/// <param name="timeProvider">Sistem zaman sağlayıcısı.</param>
public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
