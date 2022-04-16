using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events;

public class RequestedKeyInfo
{
    [Required]
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = default!;

    [Required]
    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = default!;

    [Required]
    [JsonPropertyName("sender_key")]
    public string SenderKey { get; set; } = default!;

    [Required]
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = default!;
}

[EventType(EventType)]
public class RoomKeyRequestEvent
{
    public const string EventType = "m.room_key_request";

    [Required]
    [JsonPropertyName("action")]
    public string Action { get; set; } = default!;

    [JsonPropertyName("body")]
    public RequestedKeyInfo? Body { get; set; }

    [Required]
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = default!;

    [Required]
    [JsonPropertyName("requesting_device_id")]
    public string RequestingDeviceId { get; set; } = default!;
}
