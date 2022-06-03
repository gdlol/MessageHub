using System.Text;
using System.Text.Json;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p;

public class AccountData : IAccountData
{
    private const string filterStoreName = "Filter";
    private const string roomVisibilityStoreName = "RoomVisibility";

    private readonly IStorageProvider storageProvider;

    public AccountData(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        this.storageProvider = storageProvider;
    }

    private static string GetRoomDataStoreName(string? roomId)
    {
        if (roomId is null)
        {
            return nameof(AccountData);
        }
        else
        {
            string hex = Convert.ToHexString(Encoding.UTF8.GetBytes(roomId));
            return $"{nameof(AccountData)}-{hex}";
        }
    }

    private async Task<string?> GetStringAsync(string storeName, string key)
    {
        using var store = storageProvider.GetKeyValueStore(storeName);
        return await store.GetStringAsync(key);
    }

    private async Task PutStringAsync(string storeName, string key, string value)
    {
        using var store = storageProvider.GetKeyValueStore(storeName);
        await store.PutStringAsync(key, value);
        await store.CommitAsync();
    }

    private async Task DeleteStringAsync(string storeName, string key)
    {
        using var store = storageProvider.GetKeyValueStore(storeName);
        await store.DeleteAsync(key);
        await store.CommitAsync();
    }

    public Task SaveAccountDataAsync(string? roomId, string eventType, JsonElement? value)
    {
        string storeName = GetRoomDataStoreName(roomId);
        if (value is null)
        {
            return DeleteStringAsync(storeName, eventType);
        }
        else
        {
            return PutStringAsync(storeName, eventType, JsonSerializer.Serialize(value));
        }
    }

    public async Task<JsonElement?> LoadAccountDataAsync(string? roomId, string eventType)
    {
        string storeName = GetRoomDataStoreName(roomId);
        string? json = await GetStringAsync(storeName, eventType);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }
        else
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
    }

    public async Task<(string eventType, JsonElement content)[]> LoadAccountDataAsync(
        string? roomId, Func<string, JsonElement, bool>? filter, int? limit)
    {
        string storeName = GetRoomDataStoreName(roomId);
        using var store = storageProvider.GetKeyValueStore(storeName);
        if (store.IsEmpty)
        {
            return Array.Empty<(string eventType, JsonElement content)>();
        }
        filter ??= (_, _) => true;
        async IAsyncEnumerable<(string key, JsonElement value)> GetElements()
        {
            await foreach (var (key, value) in store.GetAsyncEnumerable())
            {
                if (value.Length > 0)
                {
                    var element = JsonSerializer.Deserialize<JsonElement>(value)!;
                    yield return (key, element);
                }
            }
        }
        var events = GetElements().Where(x => filter(x.key, x.value));
        if (limit is not null)
        {
            events = events.Take(limit.Value);
        }
        var result = await events.ToArrayAsync();
        return result;
    }

    public async Task<string> SaveFilterAsync(string filter)
    {
        string filterId = Guid.NewGuid().ToString();
        await PutStringAsync(filterStoreName, filterId, filter);
        return filterId;
    }

    public Task<string?> LoadFilterAsync(string filterId)
    {
        return GetStringAsync(filterStoreName, filterId);
    }

    public Task<string?> GetRoomVisibilityAsync(string roomId)
    {
        return GetStringAsync(roomVisibilityStoreName, roomId);
    }

    public async Task<bool> SetRoomVisibilityAsync(string roomId, string visibility)
    {
        await PutStringAsync(roomVisibilityStoreName, roomId, visibility);
        return true;
    }

    public async Task<string[]> GetPublicRoomListAsync()
    {
        using var store = storageProvider.GetKeyValueStore(roomVisibilityStoreName);
        if (store.IsEmpty)
        {
            return Array.Empty<string>();
        }
        using var iterator = store.Iterate();
        var result = new List<string>();
        do
        {
            var (key, value) = iterator.CurrentValue;
            if (Encoding.UTF8.GetString(value.Span) == "public")
            {
                result.Add(key);
            }
        } while (await iterator.TryMoveAsync());
        return result.ToArray();
    }
}
