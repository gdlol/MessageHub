using System.Text;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p;

public class RoomDiscoveryService : IRoomDiscoveryService
{
    private const string aliasesStorageName = "Aliases";

    private readonly IPeerIdentity peerIdentity;
    private readonly IStorageProvider storageProvider;

    public RoomDiscoveryService(IPeerIdentity peerIdentity, IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);
        ArgumentNullException.ThrowIfNull(storageProvider);

        this.peerIdentity = peerIdentity;
        this.storageProvider = storageProvider;
    }

    public async Task<string?> GetRoomIdAsync(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);

        if (string.IsNullOrEmpty(alias))
        {
            return null;
        }
        using var store = storageProvider.GetKeyValueStore(aliasesStorageName);
        return await store.GetStringAsync(alias);
    }

    public Task<string[]> GetServersAsync(string roomId)
    {
        ArgumentNullException.ThrowIfNull(roomId);

        var result = new[] { peerIdentity.Id };
        return Task.FromResult(result);
    }

    public async Task<bool?> SetRoomAliasAsync(string roomId, string alias)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(alias);

        using var store = storageProvider.GetKeyValueStore(aliasesStorageName);
        await store.PutStringAsync(alias, roomId);
        var savedRoomId = await store.GetStringAsync(alias);
        return savedRoomId == roomId;
    }

    public async Task<bool> DeleteRoomAliasAsync(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);

        using var store = storageProvider.GetKeyValueStore(aliasesStorageName);
        await store.DeleteAsync(alias);
        return true;
    }

    public async Task<string[]> GetAliasesAsync(string roomId)
    {
        ArgumentNullException.ThrowIfNull(roomId);

        using var store = storageProvider.GetKeyValueStore(aliasesStorageName);
        var result = new List<string>();
        await foreach (var (key, value) in store.GetAsyncEnumerable())
        {
            if (Encoding.UTF8.GetString(value) == roomId)
            {
                result.Add(key);
            }
        }
        return result.ToArray();
    }
}
