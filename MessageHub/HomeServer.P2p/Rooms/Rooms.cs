using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Rooms;

namespace MessageHub.HomeServer.P2p.Rooms;

internal class Rooms : IRooms
{
    private readonly EventStore eventStore;
    private readonly IStorageProvider storageProvider;

    public Rooms(EventStore eventStore, IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(storageProvider);

        this.eventStore = eventStore;
        this.storageProvider = storageProvider;
    }

    public bool HasRoom(string roomId)
    {
        return eventStore.Update().JoinedRoomIds.Contains(roomId);
    }

    public Task<IRoomEventStore> GetRoomEventStoreAsync(string roomId)
    {
        var store = storageProvider.GetEventStore();
        IRoomEventStore roomEventStore = new RoomEventStore(eventStore.Update(), store, roomId);
        return Task.FromResult(roomEventStore);
    }

    public async Task<RoomSnapshot> GetRoomSnapshotAsync(string roomId)
    {
        using var store = storageProvider.GetEventStore();
        var snapshot = await EventStore.GetRoomSnapshotAsync(store, roomId);
        return snapshot;
    }
}
