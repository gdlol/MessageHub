using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

[EventType(EventTypes.Redact)]
public class RedactionEvent
{
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
