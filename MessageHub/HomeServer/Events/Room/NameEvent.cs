using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

[EventType(EventTypes.Name)]
public class NameEvent
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;
}
