using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.HomeServer.Formatting;

namespace MessageHub.HomeServer;

public class EventHash
{
    [Required]
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = default!;
}

public class ServerSignatures : Dictionary<KeyIdentifier, string> { }

public class Signatures : Dictionary<string, ServerSignatures> { }

public class UnsignedData
{
    [JsonPropertyName("age")]
    public long? Age { get; set; }
}

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
    public EventHash Hashes { get; set; } = default!;

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

    public string ToCanonicalJson() => CanonicalJson.Serialize(
        JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }));

    public string GetEventId() =>
        "$" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ToCanonicalJson()))) + ":" + Origin;
}
