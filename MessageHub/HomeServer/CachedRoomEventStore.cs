using System.Collections.Concurrent;
using System.Collections.Immutable;
using MessageHub.ClientServer.Protocol.Events.Room;

namespace MessageHub.HomeServer;

internal class CachedRoomEventStore : IRoomEventStore
{
    private readonly IRoomEventStore roomEventStore;
    private readonly ConcurrentDictionary<string, PersistentDataUnit> eventCache = new();
    private readonly ConcurrentDictionary<string, ImmutableList<IPeerIdentity>> identityCache = new();
    private readonly ConcurrentDictionary<string, ImmutableDictionary<RoomStateKey, string>> stateCache = new();

    public CachedRoomEventStore(IRoomEventStore roomEventStore)
    {
        ArgumentNullException.ThrowIfNull(roomEventStore);

        this.roomEventStore = roomEventStore;
    }

    public bool IsEmpty => roomEventStore.IsEmpty;

    public CreateEvent GetCreateEvent() => roomEventStore.GetCreateEvent();

    public Task<string[]> GetMissingEventIdsAsync(IEnumerable<string> eventIds)
    {
        return roomEventStore.GetMissingEventIdsAsync(eventIds);
    }

    public RoomIdentifier GetRoomId() => roomEventStore.GetRoomId();

    public async ValueTask<PersistentDataUnit> LoadEventAsync(string eventId)
    {
        if (eventCache.TryGetValue(eventId, out var pdu))
        {
            return pdu;
        }
        pdu = await roomEventStore.LoadEventAsync(eventId);
        eventCache.TryAdd(eventId, pdu);
        return pdu;
    }

    public async ValueTask<ImmutableDictionary<RoomStateKey, string>> LoadStatesAsync(string eventId)
    {
        if (stateCache.TryGetValue(eventId, out var states))
        {
            return states;
        }
        states = await roomEventStore.LoadStatesAsync(eventId);
        stateCache.TryAdd(eventId, states);
        return states;
    }
}
