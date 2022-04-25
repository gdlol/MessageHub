using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    [Required]
    [JsonPropertyName("signatures")]
    public JsonElement Signatures { get; set; } = default!;
}
