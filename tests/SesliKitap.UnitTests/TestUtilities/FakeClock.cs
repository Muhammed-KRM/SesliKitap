using SesliKitap.Business.Interfaces;

namespace SesliKitap.UnitTests.TestUtilities;

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 23, 20, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
}
