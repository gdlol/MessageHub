using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

[EventType(RoomEventTypes.Topic)]
public class TopicEvent
{
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = default!;
}
