using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Rooms;

namespace MessageHub.HomeServer.Dummy.Rooms;

public class DummyRooms : IRooms
{
    private static ImmutableDictionary<string, DummyRoomEventStore> roomEventStores;
    private static ImmutableDictionary<string, RoomSnapshot> roomSnapshots;

    static DummyRooms()
    {
        roomEventStores = ImmutableDictionary<string, DummyRoomEventStore>.Empty;
        roomSnapshots = ImmutableDictionary<string, RoomSnapshot>.Empty;
    }

    public bool HasRoom(string roomId)
    {
        return roomEventStores.ContainsKey(roomId);
    }

    public Task<IRoomEventStore> GetRoomEventStoreAsync(string roomId)
    {
        IRoomEventStore result = roomEventStores[roomId];
        return Task.FromResult(result);
    }

    public Task<RoomSnapshot> GetRoomSnapshotAsync(string roomId)
    {
        var result = roomSnapshots[roomId];
        return Task.FromResult(result);
    }

    public async Task AddEventAsync(
        string eventId,
        PersistentDataUnit pdu,
        ImmutableDictionary<RoomStateKey, string> states)
    {
        string roomId = pdu.RoomId;
        if (!HasRoom(roomId))
        {
            if (pdu.EventType != EventTypes.Create)
            {
                throw new InvalidOperationException($"{nameof(pdu.EventType)}: {pdu.EventType}");
            }
            string creator = pdu.Sender;
            roomEventStores = roomEventStores.SetItem(roomId, new DummyRoomEventStore(creator));
            roomSnapshots = roomSnapshots.SetItem(roomId, new RoomSnapshot());
        }
        var roomEventStore = roomEventStores[roomId];
        var roomSnapshot = roomSnapshots[roomId];
        roomEventStore = roomEventStore.AddEvent(eventId, pdu, states);
        var stateResolver = new RoomStateResolver(roomEventStore);
        var latestEventIds = roomSnapshot.LatestEventIds.Except(pdu.PreviousEvents).Union(new[] { eventId });
        var newStates = await stateResolver.ResolveStateAsync(latestEventIds);
        var pdus = new List<PersistentDataUnit>();
        var stateContents = new Dictionary<RoomStateKey, JsonElement>();
        foreach (string latestEventId in latestEventIds)
        {
            var latestEvent = await roomEventStore.LoadEventAsync(latestEventId);
            pdus.Add(latestEvent);
        }
        foreach (string stateEventId in newStates.Values)
        {
            var stateEvent = await roomEventStore.LoadEventAsync(stateEventId);
            stateContents[new RoomStateKey(stateEvent.EventType, stateEvent.StateKey!)] = stateEvent.Content;
        }
        roomSnapshot = new RoomSnapshot
        {
            LatestEventIds = latestEventIds.ToImmutableList(),
            GraphDepth = pdus.Select(x => x.Depth).DefaultIfEmpty(0).Max() + 1,
            States = newStates,
            StateContents = stateContents.ToImmutableDictionary()
        };
        roomEventStores = roomEventStores.SetItem(roomId, roomEventStore);
        roomSnapshots = roomSnapshots.SetItem(roomId, roomSnapshot);
    }
}
