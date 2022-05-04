using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events;

public class StrippedStateEvent
{
    [JsonPropertyName("content")]
    public JsonElement Content { get; init; } = default!;

    [JsonPropertyName("sender")]
    public string Sender { get; init; } = default!;

    [JsonPropertyName("state_key")]
    public string StateKey { get; init; } = default!;

    [JsonPropertyName("type")]
    public string EventType { get; init; } = default!;
}
