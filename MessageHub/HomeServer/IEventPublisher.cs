namespace MessageHub.HomeServer;

public interface IEventPublisher
{
    Task PublishAsync(PersistentDataUnit pdu);
}
