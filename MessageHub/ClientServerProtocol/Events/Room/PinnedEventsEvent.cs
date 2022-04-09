using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

[EventType(RoomEventTypes.PinnedEvents)]
public class PinnedEventsEvent
{
    [JsonPropertyName("pinned")]
    public string[] Pinned { get; set; } = default!;
}
