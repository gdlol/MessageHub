using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

[EventType(EventTypes.Tombstone)]
public class TombstoneEvent
{
    [Required]
    [JsonPropertyName("body")]
    public string Body { get; set; } = default!;

    [Required]
    [JsonPropertyName("replacement_room")]
    public string ReplacementRoom { get; set; } = default!;
}
