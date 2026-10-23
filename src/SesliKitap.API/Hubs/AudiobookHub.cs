using Microsoft.AspNetCore.SignalR;

namespace SesliKitap.API.Hubs;

/// <summary>
/// Canlı bildirim SignalR Hub'ı (<c>/hubs/audiobook</c>). İstemciler ilgilendikleri gruplara katılır.
/// </summary>
public sealed partial class AudiobookHub : Hub<IAudiobookClient>
{
    private readonly ILogger<AudiobookHub> _logger;

    /// <summary>
    /// <see cref="AudiobookHub"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public AudiobookHub(ILogger<AudiobookHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// İstemciyi belirli bir kitabın bildirim grubuna (<c>book-{bookId}</c>) ekler.
    /// </summary>
    public Task JoinBookGroup(Guid bookId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Book(bookId));

    /// <summary>
    /// İstemciyi belirli bir kitabın bildirim grubundan çıkarır.
    /// </summary>
    public Task LeaveBookGroup(Guid bookId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.Book(bookId));

    /// <summary>
    /// İstemciyi kütüphane bildirim grubuna (<c>library</c>) ekler.
    /// </summary>
    public Task JoinLibrary() =>
        Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Library);

    /// <summary>
    /// İstemciyi ses profilleri bildirim grubuna (<c>voices</c>) ekler.
    /// </summary>
    public Task JoinVoices() =>
        Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Voices);

    /// <inheritdoc />
    public override Task OnConnectedAsync()
    {
        LogHubConnected(_logger, Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "AudiobookHub bağlantısı kuruldu: {ConnectionId}")]
    private static partial void LogHubConnected(ILogger logger, string connectionId);
}
