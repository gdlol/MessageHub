using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Space;

[EventType(EventTypes.Parent)]
public class ParentEvent
{
    [JsonPropertyName("canonical")]
    public bool? Canonical { get; set; }

    [JsonPropertyName("via")]
    public string[]? Via { get; set; }
}
