using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

[EventType(EventTypes.PowerLevels)]
public class PowerLevelsEvent
{
    [JsonPropertyName("ban")]
    public int Ban { get; init; } = 50;

    [JsonPropertyName("events")]
    public ImmutableDictionary<string, int>? Events { get; init; }

    [JsonPropertyName("events_default")]
    public int EventsDefault { get; init; }

    [JsonPropertyName("invite")]
    public int Invite { get; init; } = 0;

    [JsonPropertyName("kick")]
    public int Kick { get; init; } = 50;

    [JsonPropertyName("notifications")]
    public ImmutableDictionary<string, int>? Notifications { get; init; }

    [JsonPropertyName("redact")]
    public int Redact { get; init; } = 50;

    [JsonPropertyName("state_default")]
    public int StateDefault { get; init; } = 50;

    [JsonPropertyName("users")]
    public ImmutableDictionary<string, int>? Users { get; init; }

    [JsonPropertyName("users_default")]
    public int? UsersDefault { get; init; }
}
