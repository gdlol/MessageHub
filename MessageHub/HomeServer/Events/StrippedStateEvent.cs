using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events;

public class StrippedStateEvent
{
    [Required]
    [JsonPropertyName("content")]
    public JsonElement Content { get; init; } = default!;

    [Required]
    [JsonPropertyName("sender")]
    public string Sender { get; init; } = default!;

    [Required]
    [JsonPropertyName("state_key")]
    public string? StateKey { get; init; }

    [Required]
    [JsonPropertyName("type")]
    public string EventType { get; init; } = default!;
}
