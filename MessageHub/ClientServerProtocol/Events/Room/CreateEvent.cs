using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

public class PreviousRoom
{
    [Required]
    [JsonPropertyName("event_id")]
    public string EventId { get; set; } = default!;

    [Required]
    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = default!;
}

[EventType(EventTypes.Create)]
public class CreateEvent
{
    [Required]
    [JsonPropertyName("creator")]
    public string Creator { get; set; } = default!;

    [JsonPropertyName("m.federate")]
    public bool Federated { get; set; } = true;

    [JsonPropertyName("predecessor")]
    public PreviousRoom? Predecessor { get; set; }

    [JsonPropertyName("room_version")]
    public string RoomVersion { get; set; } = "1";

    [JsonPropertyName("type")]
    public string? RoomType { get; set; }
}
