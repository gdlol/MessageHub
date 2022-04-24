using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events.Room;

[EventType(EventTypes.Create)]
public class CreateEvent
{
    [Required]
    [JsonPropertyName("creator")]
    public string Creator { get; init; } = default!;

    [JsonPropertyName("m.federate")]
    public bool Federated { get; init; } = true;

    [JsonPropertyName("room_version")]
    public string? RoomVersion { get; init; }

    [JsonPropertyName("type")]
    public string? RoomType { get; init; }
}
