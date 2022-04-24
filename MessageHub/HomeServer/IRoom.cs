using System.Collections.Immutable;

namespace MessageHub.HomeServer;

public interface IRoom
{
    IRoomEventStore EventStore { get; }
    ImmutableList<string> LatestEventIds { get; }
    ImmutableDictionary<RoomStateKey, string> States { get; }
}
