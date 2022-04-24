using System.Collections.Immutable;
using MessageHub.ClientServer.Protocol.Events.Room;

namespace MessageHub.HomeServer;

public interface IRoomEventStore
{
    bool IsEmpty { get; }
    RoomIdentifier GetRoomId();
    CreateEvent GetCreateEvent();
    Task<string[]> GetMissingEventIdsAsync(IEnumerable<string> eventIds);
    ValueTask<PersistentDataUnit> LoadEventAsync(string eventId);
    ValueTask<ImmutableDictionary<RoomStateKey, string>> LoadStatesAsync(string eventId);
}

public interface IEventStore
{
    bool HasRoom(string roomId);
    Task<PersistentDataUnit> LoadEventAsync(string eventId);
    ValueTask<IRoomEventStore> GetRoomEventStoreAsync(string roomId);
}
