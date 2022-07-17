using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.Federation.Protocol;

public class SignedResponse
{
    [JsonPropertyName("request")]
    public SignedRequest Request { get; set; } = default!;

    [JsonPropertyName("content")]
    public JsonElement Content { get; set; }

    [JsonPropertyName("signatures")]
    public JsonElement Signatures { get; set; } = default!;
}
