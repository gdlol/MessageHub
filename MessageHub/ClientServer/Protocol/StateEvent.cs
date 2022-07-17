using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class StateEvent
{
    [JsonPropertyName("content")]
    public JsonElement Content { get; set; } = default!;

    [JsonPropertyName("state_key")]
    public string StateKey { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string EventType { get; set; } = default!;
}
