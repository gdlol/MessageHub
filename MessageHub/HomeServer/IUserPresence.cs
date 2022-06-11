using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer;

public class PresenceStatus
{
    [JsonPropertyName("currently_active")]
    public bool? CurrentlyActive { get; init; }

    [JsonPropertyName("last_active_ago")]
    public long? LastActiveAgo { get; init; }

    [JsonPropertyName("presence")]
    public string Presence { get; init; } = default!;

    [JsonPropertyName("status_msg")]
    public string? StatusMessage { get; init; }
}

public interface IUserPresence
{
    PresenceStatus? GetPresence(string userId);
    void SetPresence(string userId, string presence, string? statusMessage);
    (string userId, PresenceStatus status)[] GetPendingUpdates(Func<string, bool> userIdFilter);
    ImmutableDictionary<string, long> GetLatestUpdateTimestamps();
}
