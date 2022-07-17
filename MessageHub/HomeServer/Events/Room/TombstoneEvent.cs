using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

[EventType(EventTypes.Tombstone)]
public class TombstoneEvent
{
    [JsonPropertyName("body")]
    public string Body { get; init; } = default!;

    [JsonPropertyName("replacement_room")]
    public string ReplacementRoom { get; init; } = default!;
}
