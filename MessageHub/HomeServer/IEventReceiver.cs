namespace MessageHub.HomeServer;

public interface IEventReceiver
{
    Task<Dictionary<string, string?>> SendPersistentEventsAsync(PersistentDataUnit[] pdus);
    Task SendEphemeralEventsAsync(EphemeralDataUnit[] eus);
}
