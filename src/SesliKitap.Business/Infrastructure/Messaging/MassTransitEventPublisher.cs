using MassTransit;
using Microsoft.Extensions.Logging;
using SesliKitap.Business.Events;
using SesliKitap.Business.Interfaces;

namespace SesliKitap.Business.Infrastructure.Messaging;

/// <summary>
/// <see cref="IEventPublisher"/> sözleşmesinin MassTransit <see cref="IPublishEndpoint"/> tabanlı uygulaması.
/// </summary>
public sealed partial class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MassTransitEventPublisher> _logger;

    /// <summary>
    /// <see cref="MassTransitEventPublisher"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public MassTransitEventPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<MassTransitEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(T integrationEvent, CancellationToken ct = default) where T : IntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _publishEndpoint.Publish(integrationEvent, ct);
        LogEventPublished(_logger, typeof(T).Name, integrationEvent.TraceId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Entegrasyon olayı yayınlandı: {EventType} (TraceId: {TraceId})")]
    private static partial void LogEventPublished(ILogger logger, string eventType, string? traceId);
}
