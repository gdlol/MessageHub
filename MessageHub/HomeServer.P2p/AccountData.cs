using System.Text;
using System.Text.Json;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p;

public class AccountData : IAccountData
{
    private const string filterStoreName = "Filter";
    private const string roomVisibilityStoreName = "RoomVisibility";
    private const string accountDataStoreName = "AccountData";

    private readonly IStorageProvider storageProvider;

    public AccountData(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        this.storageProvider = storageProvider;
    }

    private static string GetAccountDataKey(string? roomId, string eventType)
    {
        return JsonSerializer.Serialize(new[] { roomId, eventType });
    }

    private static (string? roomId, string eventType) DecodeAccountDataKey(string key)
    {
        var array = JsonSerializer.Deserialize<string?[]>(key)!;
        return (array[0], array[1]!);
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
        string key = GetAccountDataKey(roomId, eventType);
        if (value is null)
        {
            return DeleteStringAsync(accountDataStoreName, key);
        }
        else
        {
            return PutStringAsync(accountDataStoreName, key, JsonSerializer.Serialize(value));
        }
    }

    public async Task<JsonElement?> LoadAccountDataAsync(string? roomId, string eventType)
    {
        string key = GetAccountDataKey(roomId, eventType);
        string? json = await GetStringAsync(accountDataStoreName, key);
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
        using var store = storageProvider.GetKeyValueStore(accountDataStoreName);
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
                    var (elementRoomId, eventType) = DecodeAccountDataKey(key);
                    if (elementRoomId != roomId)
                    {
                        continue;
                    }
                    var element = JsonSerializer.Deserialize<JsonElement>(value)!;
                    yield return (eventType, element);
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
