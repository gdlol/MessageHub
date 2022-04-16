using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class StrippedStateEvent
{
    [Required]
    [JsonPropertyName("content")]
    public JsonElement Content { get; set; } = default!;

    [Required]
    [JsonPropertyName("sender")]
    public string Sender { get; set; } = default!;

    [Required]
    [JsonPropertyName("state_key")]
    public string? StateKey { get; set; }

    [Required]
    [JsonPropertyName("type")]
    public string EventType { get; set; } = default!;
}
