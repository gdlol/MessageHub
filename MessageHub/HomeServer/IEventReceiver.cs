using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer;

public interface IEventReceiver
{
    Task<Dictionary<string, string?>> ReceivePersistentEventsAsync(PersistentDataUnit[] pdus);
    Task ReceiveEphemeralEventsAsync(EphemeralDataUnit[] edus);
}
