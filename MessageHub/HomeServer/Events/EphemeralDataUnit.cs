using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events;

public class EphemeralDataUnit
{
    [JsonPropertyName("content")]
    public JsonElement Content { get; set; } = default!;

    [JsonPropertyName("edu_type")]
    public string EventType { get; set; } = default!;
}
