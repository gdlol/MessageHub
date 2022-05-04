using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events;

public class ServerSignatures : Dictionary<string, string> { }

public class Signatures : Dictionary<string, ServerSignatures> { }

public class PersistentDataUnit
{
    [Required]
    [JsonPropertyName("auth_events")]
    public string[] AuthorizationEvents { get; set; } = default!;

    [Required]
    [JsonPropertyName("content")]
    public JsonElement Content { get; set; } = default!;

    [Required]
    [JsonPropertyName("depth")]
    public long Depth { get; set; } = default!;

    [Required]
    [JsonPropertyName("hashes")]
    public Dictionary<string, string> Hashes { get; set; } = default!;

    [Required]
    [JsonPropertyName("origin")]
    public string Origin { get; set; } = default!;

    [Required]
    [JsonPropertyName("origin_server_ts")]
    public long OriginServerTimestamp { get; set; }

    [Required]
    [JsonPropertyName("prev_events")]
    public string[] PreviousEvents { get; set; } = default!;

    [JsonPropertyName("redacts")]
    public string? Redacts { get; set; }

    [Required]
    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = default!;

    [Required]
    [JsonPropertyName("sender")]
    public string Sender { get; set; } = default!;

    [Required]
    [JsonPropertyName("signatures")]
    public Signatures Signatures { get; set; } = default!;

    [JsonPropertyName("state_key")]
    public string? StateKey { get; set; }

    [Required]
    [JsonPropertyName("type")]
    public string EventType { get; set; } = default!;

    [JsonPropertyName("unsigned")]
    public JsonElement? Unsigned { get; set; }

    public JsonElement ToJsonElement() => JsonSerializer.SerializeToElement(this, new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    });

    public override string ToString()
    {
        return ToJsonElement().ToString();
    }
}
