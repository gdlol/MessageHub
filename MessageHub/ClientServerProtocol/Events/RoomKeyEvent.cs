using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events;

[EventType(EventType)]
public class RoomKeyEvent
{
    public const string EventType = "m.room_key";

    [Required]
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = default!;

    [Required]
    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = default!;

    [Required]
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = default!;

    [Required]
    [JsonPropertyName("session_key")]
    public string SessionKey { get; set; } = default!;
}
