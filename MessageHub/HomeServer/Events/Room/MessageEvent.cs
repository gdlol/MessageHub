using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

[EventType(EventTypes.Message)]
public class MessageEvent
{
    [JsonPropertyName("body")]
    public string Body { get; init; } = default!;

    [JsonPropertyName("msgtype")]
    public string MessageType { get; init; } = default!;
}
