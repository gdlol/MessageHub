using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.Remote;

public interface IEventPublisher
{
    Task PublishAsync(PersistentDataUnit pdu);
}
