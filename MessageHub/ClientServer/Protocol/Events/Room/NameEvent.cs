using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events.Room;

[EventType(EventTypes.Name)]
public class NameEvent
{
    [Required]
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;
}
