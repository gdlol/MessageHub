using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol;

public class StateEvent
{
    [Required]
    [JsonPropertyName("content")]
    public JsonElement Content { get; set; } = default!;

    [JsonPropertyName("state_key")]
    public string StateKey { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("type")]
    public string EventType { get; set; } = default!;
}
