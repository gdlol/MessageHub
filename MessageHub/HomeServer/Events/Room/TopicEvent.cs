using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

[EventType(EventTypes.Topic)]
public class TopicEvent
{
    [JsonPropertyName("topic")]
    public string Topic { get; init; } = default!;
}
