namespace MessageHub.HomeServer;

public interface IMissingEventsResolver
{
    Task ResolveMessingEventsAsync(string roomId, IEnumerable<string> eventIds);
}
