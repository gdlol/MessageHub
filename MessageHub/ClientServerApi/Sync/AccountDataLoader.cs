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

    public async Task<AccountData> GetAccountDataAsync(string userId, EventFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(userId);

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
            filterFunc = (eventType, _) => filters.All(x => x(eventType, _));
        }
        var accoutData = await persistenceService.LoadAccountDataAsync(null, filterFunc, limit);
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
}
