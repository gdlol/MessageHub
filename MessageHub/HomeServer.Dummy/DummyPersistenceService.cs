using System.Collections.Concurrent;
using System.Text.Json;

namespace MessageHub.HomeServer.Dummy;

using DataMap = ConcurrentDictionary<string, JsonElement>;

public class DummyPersistenceService : IPersistenceService
{
    private class AccountData
    {
        public DataMap Data { get; } = new();
        public ConcurrentDictionary<string, DataMap> RoomData { get; } = new();
    }

    private readonly ConcurrentDictionary<string, AccountData> userAccountData = new();
    private readonly ConcurrentDictionary<(string, string), string> filters = new();

    public Task SaveAccountDataAsync(string userId, string? roomId, string eventType, JsonElement? content)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(eventType);

        var accountData = userAccountData.GetOrAdd(userId, _ => new());
        var dataMap = roomId is null ? accountData.Data : accountData.RoomData.GetOrAdd(roomId, _ => new());
        if (content is null)
        {
            dataMap.TryRemove(eventType, out var _);
        }
        else
        {
            dataMap.AddOrUpdate(eventType, content.Value, (_, _) => content.Value);
        }
        return Task.CompletedTask;
    }

    public Task<JsonElement?> LoadAccountDataAsync(string userId, string? roomId, string eventType)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(eventType);

        JsonElement? result = null;
        if (userAccountData.TryGetValue(userId, out var accountData))
        {
            if (roomId is null)
            {
                if (accountData.Data.TryGetValue(eventType, out var content))
                {
                    result = content;
                }
            }
            else
            {
                if (accountData.RoomData.TryGetValue(roomId, out var dataMap)
                    && dataMap.TryGetValue(eventType, out var content))
                {
                    result = content;
                }
            }
        }
        return Task.FromResult(result);
    }

    public Task<string> SaveFilterAsync(string userId, string filter)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(filter);

        string filterId = Guid.NewGuid().ToString();
        if (!filters.TryAdd((userId, filterId), filter))
        {
            throw new InvalidOperationException();
        }
        return Task.FromResult(filterId);
    }

    public Task<string?> LoadFilterAsync(string userId, string filterId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(filterId);

        filters.TryGetValue((userId, filterId), out string? filter);
        return Task.FromResult(filter);
    }
}
