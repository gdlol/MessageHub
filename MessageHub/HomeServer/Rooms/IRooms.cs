namespace MessageHub.HomeServer.Rooms;

public interface IRooms
{
    bool HasRoom(string roomId);
    Task<RoomSnapshot> GetRoomSnapshotAsync(string roomId);
    Task<IRoomEventStore> GetRoomEventStoreAsync(string roomId);
}
