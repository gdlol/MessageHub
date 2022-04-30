using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer;

public interface IEventPublisher
{
    Task PublishAsync(PersistentDataUnit pdu);
}
