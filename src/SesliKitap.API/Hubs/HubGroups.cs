namespace SesliKitap.API.Hubs;

/// <summary>
/// SignalR <see cref="AudiobookHub"/> grup adı sabitleri ve üreticileri.
/// </summary>
public static class HubGroups
{
    /// <summary>
    /// Kütüphane ekranını izleyen istemcilerin grubu.
    /// </summary>
    public const string Library = "library";

    /// <summary>
    /// Ses profilleri ekranını izleyen istemcilerin grubu.
    /// </summary>
    public const string Voices = "voices";

    /// <summary>
    /// Belirli bir kitabın detay veya oynatıcı ekranını izleyen istemcilerin grubu (<c>book-{id}</c>).
    /// </summary>
    public static string Book(Guid bookId) => $"book-{bookId:D}";
}
