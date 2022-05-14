using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Rooms;

namespace MessageHub.HomeServer.P2p.Rooms;

public class Rooms : IRooms
{
    private const string creatorStoreName = "Creators";
    private const string SnapshotKey = "Snapshot";

    private readonly IStorageProvider storageProvider;

    public Rooms(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        this.storageProvider = storageProvider;
    }

    public static string GetStoreName(string roomId)
    {
        string hex = Convert.ToHexString(Encoding.UTF8.GetBytes(roomId));
        return $"Room-{hex}";
    }

    public bool HasRoom(string roomId)
    {
        return storageProvider.HasKeyValueStore(GetStoreName(roomId));
    }

    private async ValueTask<string> GetCreatorAsync(string roomId)
    {
        using var creatorStore = storageProvider.GetKeyValueStore(creatorStoreName);
        string? creator = await creatorStore.GetStringAsync(roomId);
        if (creator is null)
        {
            throw new InvalidOperationException();
        }
        return creator;
    }

    private async ValueTask SetCreatorAsync(string roomId, string creator)
    {
        using var creatorStore = storageProvider.GetKeyValueStore(creatorStoreName);
        await creatorStore.PutStringAsync(roomId, creator);
    }

    public async Task<RoomEventStore> GetRoomEventStoreAsync(string roomId)
    {
        string creator = await GetCreatorAsync(roomId);
        string storeName = GetStoreName(roomId);
        var store = storageProvider.GetKeyValueStore(storeName);
        return new RoomEventStore(creator, store);
    }

    private static async ValueTask<RoomSnapshot> GetRoomSnapshotAsync(IKeyValueStore store)
    {
        var snapshotBytes = await store.GetAsync(SnapshotKey);
        var snapshot = JsonSerializer.Deserialize<RoomSnapshot>(snapshotBytes);
        return snapshot ?? new RoomSnapshot();
    }

    public async Task<RoomSnapshot> GetRoomSnapshotAsync(string roomId)
    {
        string storeName = GetStoreName(roomId);
        using var store = storageProvider.GetKeyValueStore(storeName);
        var snapshot = await GetRoomSnapshotAsync(store);
        return snapshot;
    }

    public async Task SetRoomSnapshotAsync(string roomId, RoomSnapshot snapshot)
    {
        string storeName = GetStoreName(roomId);
        using var store = storageProvider.GetKeyValueStore(storeName);
        var snapshotBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);
        await store.PutAsync(SnapshotKey, snapshotBytes);
    }

    async Task<IRoomEventStore> IRooms.GetRoomEventStoreAsync(string roomId)
    {
        IRoomEventStore result = await GetRoomEventStoreAsync(roomId);
        return result;
    }

    public async ValueTask AddEventAsync(
        string eventId,
        PersistentDataUnit pdu,
        ImmutableDictionary<RoomStateKey, string> states)
    {
        string roomId = pdu.RoomId;
        string storeName = GetStoreName(roomId);
        if (!storageProvider.HasKeyValueStore(storeName))
        {
            if (pdu.EventType != EventTypes.Create)
            {
                throw new InvalidOperationException($"{nameof(pdu.EventType)}: {pdu.EventType}");
            }
            string creator = pdu.Sender;
            await SetCreatorAsync(roomId, creator);
            await SetRoomSnapshotAsync(roomId, new RoomSnapshot());
        }
        {
            string creator = await GetCreatorAsync(roomId);
            using var store = storageProvider.GetKeyValueStore(storeName);
            var roomEventStore = new RoomEventStore(creator, store);
            var roomSnapshot = await GetRoomSnapshotAsync(store);
            await roomEventStore.AddEvent(eventId, pdu, states);

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
                GraphDepth = pdus.Select(x => x.Depth).Max(),
                States = newStates,
                StateContents = stateContents.ToImmutableDictionary()
            };
            await SetRoomSnapshotAsync(roomId, roomSnapshot);
            await store.CommitAsync();
        }
    }
}
