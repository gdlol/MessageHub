using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.General;

public static class PresenceValues
{
    public const string Offline = "offline";
    public const string Online = "online";
    public const string Unavailable = "unavailable";
}

[EventType(EventType)]
public class PresenceEvent
{
    public const string EventType = "m.presence";

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }

    [JsonPropertyName("currently_active")]
    public bool? CurrentlyActive { get; init; }

    [JsonPropertyName("displayname")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("last_active_ago")]
    public long? LastActiveAgo { get; init; }

    [JsonPropertyName("presence")]
    public string Presence { get; init; } = default!;

    [JsonPropertyName("status_msg")]
    public string? StatusMessage { get; init; }
}

public class UserPresenceUpdate
{
    [JsonPropertyName("currently_active")]
    public bool? CurrentlyActive { get; init; }

    [JsonPropertyName("last_active_ago")]
    public long? LastActiveAgo { get; init; }

    [JsonPropertyName("presence")]
    public string Presence { get; init; } = default!;

    [JsonPropertyName("status_msg")]
    public string? StatusMessage { get; init; }

    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = default!;

    public static UserPresenceUpdate Create(string userId, PresenceStatus status)
    {
        return new UserPresenceUpdate
        {
            CurrentlyActive = status.CurrentlyActive,
            LastActiveAgo = status.LastActiveAgo,
            Presence = status.Presence,
            StatusMessage = status.StatusMessage,
            UserId = userId
        };
    }
}

public class PresenceUpdate
{
    [JsonPropertyName("push")]
    public UserPresenceUpdate[] Push { get; init; } = default!;
}
