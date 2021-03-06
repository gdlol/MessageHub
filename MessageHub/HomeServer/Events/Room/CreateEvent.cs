using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

[EventType(EventTypes.Create)]
public class CreateEvent
{
    [JsonPropertyName("creator")]
    public string Creator { get; init; } = default!;

    [JsonPropertyName("m.federate")]
    public bool Federated { get; init; } = true;

    [JsonPropertyName("room_version")]
    public string? RoomVersion { get; init; }

    [JsonPropertyName("type")]
    public string? RoomType { get; init; }
}
