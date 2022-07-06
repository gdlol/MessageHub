using MessageHub.HomeServer.Rooms;

namespace MessageHub.HomeServer.P2p.Rooms;

internal class Rooms : IRooms
{
    private readonly EventStore eventStore;

    public Rooms(EventStore eventStore)
    {
        ArgumentNullException.ThrowIfNull(eventStore);

        this.eventStore = eventStore;
    }

    public bool HasRoom(string roomId)
    {
        return eventStore.LoadState().JoinedRoomIds.Contains(roomId);
    }

    public Task<IRoomEventStore> GetRoomEventStoreAsync(string roomId)
    {
        var session = eventStore.GetReadOnlySession();
        IRoomEventStore roomEventStore = new RoomEventStore(session, roomId);
        return Task.FromResult(roomEventStore);
    }

    public async Task<RoomSnapshot> GetRoomSnapshotAsync(string roomId)
    {
        var session = eventStore.GetReadOnlySession();
        var snapshot = await session.GetRoomSnapshotAsync(roomId);
        return snapshot;
    }
}
