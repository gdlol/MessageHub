using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.HomeServer;

namespace MessageHub.Federation.Protocol;

public class SignedRequest
{
    [Required]
    [JsonPropertyName("method")]
    public string Method { get; set; } = default!;

    [Required]
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = default!;

    [Required]
    [JsonPropertyName("origin")]
    public string Origin { get; set; } = default!;

    [Required]
    [JsonPropertyName("origin_server_ts")]
    public long OriginServerTimestamp { get; set; }

    [Required]
    [JsonPropertyName("destination")]
    public string Destination { get; set; } = default!;

    [JsonPropertyName("content")]
    public JsonElement? Content { get; set; }

    [JsonPropertyName("server_keys")]
    public ServerKeys ServerKeys { get; set; } = default!;

    [Required]
    [JsonPropertyName("signatures")]
    public JsonElement Signatures { get; set; } = default!;

    public JsonElement ToJsonElement() => JsonSerializer.SerializeToElement(this, new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    });

    public override string ToString()
    {
        return ToJsonElement().ToString();
    }
}
