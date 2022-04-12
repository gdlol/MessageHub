using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer;

public class EphemeralDataUnit
{
    [Required]
    [JsonPropertyName("content")]
    public JsonElement Content { get; set; } = default!;

    [Required]
    [JsonPropertyName("edu_type")]
    public string EventType { get; set; } = default!;
}
