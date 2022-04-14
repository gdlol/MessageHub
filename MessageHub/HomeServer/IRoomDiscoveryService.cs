namespace MessageHub.HomeServer;

public interface IRoomDiscoveryService
{
    Task<string?> GetRoomIdAsync(string alias);
    Task<string[]> GetServersAsync(string roomId);
    Task<bool?> SetRoomAliasAsync(string roomId, string alias);
    Task<bool> DeleteRoomAliasAsync(string alias);
    Task<string[]> GetAliasesAsync(string roomId);
}
