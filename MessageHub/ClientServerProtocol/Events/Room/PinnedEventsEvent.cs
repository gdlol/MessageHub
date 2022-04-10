using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

[EventType(EventTypes.PinnedEvents)]
public class PinnedEventsEvent
{
    [Required]
    [JsonPropertyName("pinned")]
    public string[] Pinned { get; set; } = default!;
}
