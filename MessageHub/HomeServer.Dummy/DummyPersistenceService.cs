using System.Collections.Concurrent;
using System.Text.Json;

namespace MessageHub.HomeServer.Dummy;

using DataMap = ConcurrentDictionary<string, JsonElement>;

public class DummyPersistenceService : IPersistenceService
{
    private readonly DataMap accountData = new();
    private readonly ConcurrentDictionary<string, DataMap> roomData = new();
    private readonly ConcurrentDictionary<string, string> filters = new();

    public Task SaveAccountDataAsync(string? roomId, string eventType, JsonElement? content)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        var dataMap = roomId is null ? accountData : roomData.GetOrAdd(roomId, _ => new());
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

    public Task<JsonElement?> LoadAccountDataAsync(string? roomId, string eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        JsonElement? result = null;
        if (roomId is null)
        {
            if (accountData.TryGetValue(eventType, out var content))
            {
                result = content;
            }
        }
        else
        {
            if (roomData.TryGetValue(roomId, out var dataMap)
                && dataMap.TryGetValue(eventType, out var content))
            {
                result = content;
            }
        }
        return Task.FromResult(result);
    }

    public Task<(string eventType, JsonElement content)[]> LoadAccountDataAsync(
        string? roomId,
        Func<string, JsonElement, bool>? filter,
        int? limit)
    {
        (string eventType, JsonElement content)[] result;
        var dataMap = roomId is null
            ? accountData
            : roomData.TryGetValue(roomId, out var value) ? value : null;
        if (dataMap is null)
        {
            result = Array.Empty<(string eventType, JsonElement content)>();
        }
        else
        {
            filter ??= (_, _) => true;
            var events = from pair in dataMap
                         where filter(pair.Key, pair.Value)
                         select (pair.Key, pair.Value);
            if (limit is not null)
            {
                events = events.Take(limit.Value);
            }
            result = events.ToArray();
        }
        return Task.FromResult(result);
    }

    public Task<string> SaveFilterAsync(string filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        string filterId = Guid.NewGuid().ToString();
        if (!filters.TryAdd(filterId, filter))
        {
            throw new InvalidOperationException();
        }
        return Task.FromResult(filterId);
    }

    public Task<string?> LoadFilterAsync(string filterId)
    {
        ArgumentNullException.ThrowIfNull(filterId);

        filters.TryGetValue(filterId, out string? filter);
        return Task.FromResult(filter);
    }
}