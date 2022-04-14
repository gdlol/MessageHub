using System.Collections.Concurrent;

namespace MessageHub.HomeServer.Dummy;

public class DummyRoomDiscoveryService : IRoomDiscoveryService
{
    private readonly ConcurrentDictionary<string, string> aliases = new();

    public Task<string?> GetRoomIdAsync(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);

        string? roomId = aliases.TryGetValue(alias, out var value) ? value : null;
        return Task.FromResult(roomId);
    }

    public Task<string[]> GetServersAsync(string roomId)
    {
        ArgumentNullException.ThrowIfNull(roomId);

        var result = new[] { DummyHostInfo.Instance.ServerName };
        return Task.FromResult(result);
    }

    public Task<bool?> SetRoomAliasAsync(string roomId, string alias)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(alias);

        bool? result;
        if (RoomHistory.RoomStatesList[^1].Rooms.ContainsKey(roomId))
        {
            result = aliases.TryAdd(alias, roomId);
        }
        else
        {
            result = null;
        }
        return Task.FromResult(result);
    }

    public Task<bool> DeleteRoomAliasAsync(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);

        bool result = aliases.TryRemove(alias, out var _);
        return Task.FromResult(result);
    }

    public Task<string[]> GetAliasesAsync(string roomId)
    {
        var result = aliases.Where(x => x.Value == roomId).Select(x => x.Key).ToArray();
        return Task.FromResult(result);
    }
}
