using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol;

public class ClientEventWithoutRoomID
{
    [Required]
    [JsonPropertyName("content")]
    public JsonElement Content { get; set; } = default!;

    [Required]
    [JsonPropertyName("event_id")]
    public string EventId { get; set; } = default!;

    [Required]
    [JsonPropertyName("origin_server_ts")]
    public long OriginServerTimestamp { get; set; }

    [Required]
    [JsonPropertyName("sender")]
    public string Sender { get; set; } = default!;

    [JsonPropertyName("state_key")]
    public string? StateKey { get; set; }

    [Required]
    [JsonPropertyName("type")]
    public string EventType { get; set; } = default!;

    [JsonPropertyName("unsigned")]
    public JsonElement? Unsigned { get; set; }
}
