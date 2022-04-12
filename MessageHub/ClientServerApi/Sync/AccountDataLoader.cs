using System.Text.Json;
using MessageHub.ClientServerProtocol;
using MessageHub.HomeServer;

namespace MessageHub.ClientServerApi.Sync;

public class AccountDataLoader
{
    private readonly IPersistenceService persistenceService;

    public AccountDataLoader(IPersistenceService persistenceService)
    {
        ArgumentNullException.ThrowIfNull(persistenceService);

        this.persistenceService = persistenceService;
    }

    private async Task<AccountData> InternalLoadAccountDataAsync(
        string userId,
        string? roomId,
        EventFilter? filter,
        bool? containsUrl = null)
    {
        int? limit = filter?.Limit;
        Func<string, JsonElement, bool>? filterFunc = null;
        var result = new AccountData
        {
            Events = Array.Empty<Event>()
        };
        if (filter is not null)
        {
            var filters = new List<Func<string, JsonElement, bool>>();
            if (filter.Senders is not null && !filter.Senders.Contains(userId))
            {
                return result;
            }
            if (filter.NotSenders is not null && filter.NotSenders.Contains(userId))
            {
                return result;
            }
            if (filter.Types is not null)
            {
                if (filter.Types.Length == 0)
                {
                    return result;
                }
                else
                {
                    filters.Add((eventType, _) => filter.Types.Any(pattern => Filter.StringMatch(eventType, pattern)));
                }
            }
            if (filter.NotTypes is not null)
            {
                filters.Add((eventType, _) => !filter.NotTypes.Any(pattern => Filter.StringMatch(eventType, pattern)));
            }
            if (containsUrl is not null)
            {
                filters.Add((_, content) => content.TryGetProperty("url", out var _) == containsUrl.Value);
            }
            filterFunc = (eventType, _) => filters.All(x => x(eventType, _));
        }
        var accoutData = await persistenceService.LoadAccountDataAsync(roomId, filterFunc, limit);
        var events = new List<Event>();
        foreach (var (eventType, content) in accoutData)
        {
            events.Add(new Event
            {
                Content = content,
                EventType = eventType
            });
        }
        result.Events = events.ToArray();
        return result;
    }

    public Task<AccountData> LoadAccountDataAsync(string userId, EventFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return InternalLoadAccountDataAsync(userId, null, filter);
    }

    public Task<AccountData> LoadAccountDataAsync(string userId, string roomId, RoomEventFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var eventFilter = filter is null ? null : new EventFilter
        {
            Limit = filter.Limit,
            NotSenders = filter.NotSenders,
            NotTypes = filter.NotTypes,
            Senders = filter.Senders,
            Types = filter.Types,
        };
        return InternalLoadAccountDataAsync(userId, roomId, eventFilter, filter?.ContainsUrl);
    }
}
