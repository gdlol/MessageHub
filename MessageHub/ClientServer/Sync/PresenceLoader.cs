using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events.General;
using MessageHub.Serialization;

namespace MessageHub.ClientServer.Sync;

public class PresenceLoader
{
    private readonly IUserPresence userPresence;

    public PresenceLoader(IUserPresence userPresence)
    {
        ArgumentNullException.ThrowIfNull(userPresence);

        this.userPresence = userPresence;
    }

    private static Func<string, bool> GetUserIdFilter(EventFilter? filter)
    {
        if (filter is not null)
        {
            return userId =>
            {
                if (filter.NotSenders is not null && filter.NotSenders.Contains(userId))
                {
                    return false;
                }
                if (filter.Senders is not null && !filter.Senders.Contains(userId))
                {
                    return false;
                }
                return true;
            };
        }
        return _ => true;
    }

    public Presence? LoadPresenceUpdates(EventFilter? filter)
    {
        var result = new List<Event>();
        var userIdFilter = GetUserIdFilter(filter);
        var updates = userPresence.GetPendingUpdates(userIdFilter);
        foreach (var (userId, presenceStatus) in updates)
        {
            result.Add(new Event
            {
                Sender = userId,
                EventType = PresenceEvent.EventType,
                Content = DefaultJsonSerializer.SerializeToElement(new PresenceEvent
                {
                    CurrentlyActive = presenceStatus.CurrentlyActive,
                    LastActiveAgo = presenceStatus.LastActiveAgo,
                    Presence = presenceStatus.Presence,
                    StatusMessage = presenceStatus.StatusMessage,
                })
            });
        }
        return result.Count == 0 ? null : new Presence { Events = result.ToArray() };
    }
}
