using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.Federation.Protocol;

public class SignedResponse
{
    [Required]
    [JsonPropertyName("request")]
    public SignedRequest Request { get; set; } = default!;

    [Required]
    [JsonPropertyName("content")]
    public JsonElement Content { get; set; }

    [Required]
    [JsonPropertyName("signatures")]
    public JsonElement Signatures { get; set; } = default!;
}
