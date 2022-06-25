using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace MessageHub.HomeServer;

public class UserPresence : IUserPresence
{
    private readonly ConcurrentDictionary<string, PresenceStatus> presenceStatus = new();
    private readonly ConcurrentDictionary<string, PresenceStatus> pendingUpdates = new();
    private readonly ConcurrentDictionary<string, long> latestUpdateTime = new();

    public PresenceStatus? GetPresence(string userId)
    {
        if (presenceStatus.TryGetValue(userId, out var status))
        {
            return status;
        }
        return null;
    }

    public void SetPresence(string userId, string presence, string? statusMessage)
    {
        var status = new PresenceStatus
        {
            Presence = presence,
            StatusMessage = statusMessage,
        };
        presenceStatus[userId] = status;
        pendingUpdates[userId] = status;
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        latestUpdateTime[userId] = timestamp;
    }

    public (string userId, PresenceStatus status)[] GetPendingUpdates(Func<string, bool> userIdFilter)
    {
        ArgumentNullException.ThrowIfNull(userIdFilter);

        var updates = new List<(string userId, PresenceStatus status)>();
        foreach (string userId in pendingUpdates.Keys.ToArray())
        {
            if (!userIdFilter(userId))
            {
                continue;
            }
            if (pendingUpdates.TryRemove(userId, out var status))
            {
                updates.Add((userId, status));
            }
        }
        return updates.ToArray();
    }

    public ImmutableDictionary<string, long> GetLatestUpdateTimestamps()
    {
        return latestUpdateTime.ToImmutableDictionary();
    }
}
