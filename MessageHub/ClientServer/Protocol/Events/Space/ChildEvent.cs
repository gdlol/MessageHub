using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events.Space;

[EventType(EventTypes.Child)]
public class ChildEvent
{
    [JsonPropertyName("order")]
    public string? Order { get; set; }

    [JsonPropertyName("suggested")]
    public bool? Suggested { get; set; }

    [JsonPropertyName("via")]
    public string[]? Via { get; set; }
}
