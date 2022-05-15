using System.Collections.Immutable;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Rooms;

namespace MessageHub.HomeServer.P2p.Rooms;

internal sealed class RoomEventStore : IRoomEventStore
{
    private readonly IKeyValueStore store;
    private readonly string roomId;
    private readonly bool ownsStore;

    public string Creator { get; }

    public RoomEventStore(EventStore eventStore, IKeyValueStore store, string roomId, bool ownsStore = true)
    {
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(store);

        this.store = store;
        this.roomId = roomId;
        this.ownsStore = ownsStore;
        Creator = eventStore.RoomCreators[roomId];
    }

    public async Task<string[]> GetMissingEventIdsAsync(IEnumerable<string> eventIds)
    {
        var result = new List<string>();
        foreach (string eventId in eventIds)
        {
            var value = await store.GetAsync(EventStore.GetEventKey(roomId, eventId));
            if (value is null)
            {
                result.Add(eventId);
            }
        }
        return result.ToArray();
    }

    public async ValueTask<PersistentDataUnit> LoadEventAsync(string eventId)
    {
        var value = await EventStore.GetEventAsync(store, roomId, eventId);
        if (value is null)
        {
            throw new KeyNotFoundException(eventId);
        }
        return value;
    }

    public async ValueTask<ImmutableDictionary<RoomStateKey, string>> LoadStatesAsync(string eventId)
    {
        var value = await EventStore.GetStatesAsync(store, roomId, eventId);
        if (value is null)
        {
            throw new KeyNotFoundException(eventId);
        }
        return value;
    }

    public void Dispose()
    {
        if (ownsStore)
        {
            store.Dispose();
        }
    }
}
