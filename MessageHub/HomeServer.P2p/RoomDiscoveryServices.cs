using System.Text;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p;

public class RoomDiscoveryService : IRoomDiscoveryService
{
    private const string aliasesStorageName = "Aliases";

    private readonly IIdentityService identityService;
    private readonly IStorageProvider storageProvider;

    public RoomDiscoveryService(IIdentityService identityService, IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(storageProvider);

        this.identityService = identityService;
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

        var result = new[] { identityService.GetSelfIdentity().Id };
        return Task.FromResult(result);
    }

    public async Task<bool?> SetRoomAliasAsync(string roomId, string alias)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(alias);

        using var store = storageProvider.GetKeyValueStore(aliasesStorageName);
        await store.PutStringAsync(alias, roomId);
        await store.CommitAsync();
        var savedRoomId = await store.GetStringAsync(alias);
        return savedRoomId == roomId;
    }

    public async Task<bool> DeleteRoomAliasAsync(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);

        using var store = storageProvider.GetKeyValueStore(aliasesStorageName);
        await store.DeleteAsync(alias);
        await store.CommitAsync();
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
