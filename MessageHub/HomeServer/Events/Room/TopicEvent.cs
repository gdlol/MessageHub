using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

[EventType(EventTypes.Topic)]
public class TopicEvent
{
    [Required]
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = default!;
}
