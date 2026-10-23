using SesliKitap.Business.Events;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Entegrasyon olaylarını mesaj kuyruğuna (RabbitMQ / MassTransit) yayınlayan soyutlama.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Belirtilen entegrasyon olayını kuyruğa yayınlar.
    /// </summary>
    /// <typeparam name="T">Olay tipi (<see cref="IntegrationEvent"/> türevi).</typeparam>
    /// <param name="integrationEvent">Yayınlanacak olay nesnesi.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task PublishAsync<T>(T integrationEvent, CancellationToken ct = default) where T : IntegrationEvent;
}
