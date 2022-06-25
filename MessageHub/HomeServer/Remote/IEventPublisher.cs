using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.Remote;

public interface IEventPublisher
{
    Task PublishAsync(PersistentDataUnit pdu);
    Task PublishAsync(string roomId, EphemeralDataUnit edu);
}
