using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

[EventType(RoomEventTypes.Name)]
public class NameEvent
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;
}
