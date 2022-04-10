using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events;

[EventType(EventType)]
public class RoomKeyWithheldEvent
{
    public const string EventType = "m.room_key.withheld";

    [Required]
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = default!;

    [Required]
    [JsonPropertyName("code")]
    public string Code { get; set; } = default!;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("room_id")]
    public string? RoomId { get; set; }

    [Required]
    [JsonPropertyName("sender_key")]
    public string SenderKey { get; set; } = default!;

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}
