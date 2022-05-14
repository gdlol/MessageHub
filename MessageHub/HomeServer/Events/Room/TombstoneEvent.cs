using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

[EventType(EventTypes.Tombstone)]
public class TombstoneEvent
{
    [Required]
    [JsonPropertyName("body")]
    public string Body { get; init; } = default!;

    [Required]
    [JsonPropertyName("replacement_room")]
    public string ReplacementRoom { get; init; } = default!;
}