using System.Collections.Immutable;

namespace MessageHub.HomeServer.Dummy;

public class DummyEventStore : IEventStore
{
    public ImmutableDictionary<string, DummyRoomEventStore> RoomEventStores { get; }

    public DummyEventStore()
    {
        RoomEventStores = ImmutableDictionary<string, DummyRoomEventStore>.Empty;
    }

    private DummyEventStore(ImmutableDictionary<string, DummyRoomEventStore> roomEventStores)
    {
        RoomEventStores = roomEventStores;
    }

    public DummyEventStore AddEvent(
        string eventId,
        PersistentDataUnit pdu,
        ImmutableDictionary<RoomStateKey, string> states)
    {
        ArgumentNullException.ThrowIfNull(pdu);

        string roomId = pdu.RoomId;
        if (!RoomEventStores.TryGetValue(roomId, out var roomEventStore))
        {
            roomEventStore = new DummyRoomEventStore();
        }
        roomEventStore = roomEventStore.AddEvent(eventId, pdu, states);
        var roomEventStores = RoomEventStores.SetItem(roomId, roomEventStore);
        return new DummyEventStore(roomEventStores);
    }

    public bool HasRoom(string roomId)
    {
        return RoomEventStores.ContainsKey(roomId);
    }

    public async Task<PersistentDataUnit> LoadEventAsync(string eventId)
    {
        var eventIds = new[] { eventId };
        foreach (var roomEventStore in RoomEventStores.Values)
        {
            var missingEventIds = await roomEventStore.GetMissingEventIdsAsync(eventIds);
            if (missingEventIds.Length == 0)
            {
                return await roomEventStore.LoadEventAsync(eventId);
            }
        }
        throw new InvalidOperationException();
    }

    public ValueTask<IRoomEventStore> GetRoomEventStoreAsync(string roomId)
    {
        IRoomEventStore result = RoomEventStores[roomId];
        return ValueTask.FromResult(result);
    }
}
