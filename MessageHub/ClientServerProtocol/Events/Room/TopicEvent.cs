using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

[EventType(EventTypes.Topic)]
public class TopicEvent
{
    [Required]
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = default!;
}
