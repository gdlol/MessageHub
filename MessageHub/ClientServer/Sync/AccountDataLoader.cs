using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;

namespace MessageHub.ClientServer.Sync;

public class AccountDataLoader
{
    private readonly IAccountData accountData;

    public AccountDataLoader(IAccountData accountData)
    {
        ArgumentNullException.ThrowIfNull(accountData);

        this.accountData = accountData;
    }

    public async Task<AccountData> LoadAccountDataAsync(string userId, EventFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var data = accountData.LoadAccountDataAsync(null)
            .Where(value => filter.ShouldIncludeEvent(userId, value.eventType))
            .ApplyLimit(filter);
        var events = new List<Event>();
        await foreach (var (eventType, content) in data)
        {
            events.Add(new Event
            {
                Content = content,
                EventType = eventType
            });
        }
        return new AccountData
        {
            Events = events.ToArray()
        };
    }

    public async Task<AccountData> LoadAccountDataAsync(string userId, string roomId, RoomEventFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (!filter.ShouldIncludeRoomId(roomId))
        {
            return new AccountData
            {
                Events = Array.Empty<Event>()
            };
        }

        var data = accountData.LoadAccountDataAsync(null)
            .Where(value => filter.ShouldIncludeEvent(userId, value.eventType, value.content))
            .ApplyLimit(filter);
        var events = new List<Event>();
        await foreach (var (eventType, content) in data)
        {
            events.Add(new Event
            {
                Content = content,
                EventType = eventType
            });
        }
        return new AccountData
        {
            Events = events.ToArray()
        };
    }
}
