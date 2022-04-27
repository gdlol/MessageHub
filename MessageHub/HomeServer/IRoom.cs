using System.Collections.Immutable;
using MessageHub.ClientServer.Protocol.Events.Room;

namespace MessageHub.HomeServer;

public interface IRoom
{
    IRoomEventStore EventStore { get; }
    ImmutableList<string> LatestEventIds { get; }
    ImmutableDictionary<RoomStateKey, string> States { get; }
    ImmutableDictionary<RoomStateKey, object> StateContents { get; }
    CreateEvent CreateEvent { get; }
    ImmutableDictionary<string, MemberEvent> Members { get; }
}
