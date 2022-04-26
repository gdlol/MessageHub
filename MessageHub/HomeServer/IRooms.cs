namespace MessageHub.HomeServer;

public interface IRooms
{
    bool HasRoom(string roomId);
    Task<IRoom> GetRoomAsync(string roomId);
}
