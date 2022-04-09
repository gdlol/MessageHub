using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

[EventType(RoomEventTypes.PowerLevels)]
public class PowerLevelsEvent
{
    [JsonPropertyName("ban")]
    public int Ban { get; set; } = 50;

    [JsonPropertyName("events")]
    public object? Events { get; set; }

    [JsonPropertyName("events_default")]
    public int EventsDefault { get; set; } = 50;

    [JsonPropertyName("invite")]
    public int Invite { get; set; } = 50;

    [JsonPropertyName("kick")]
    public int Kick { get; set; } = 50;

    [JsonPropertyName("notifications")]
    public Dictionary<string, int>? Notifications { get; set; }

    [JsonPropertyName("redact")]
    public int Redact { get; set; } = 50;

    [JsonPropertyName("state_default")]
    public int StateDefault { get; set; } = 50;

    [JsonPropertyName("users")]
    public Dictionary<string, int>? Users { get; set; }

    [JsonPropertyName("users_default")]
    public int UsersDefault { get; set; } = 50;
}
