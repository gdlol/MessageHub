using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.HomeServer;

namespace MessageHub.Federation.Protocol;

public class SignedRequest
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = default!;

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = default!;

    [JsonPropertyName("origin")]
    public string Origin { get; set; } = default!;

    [JsonPropertyName("origin_server_ts")]
    public long OriginServerTimestamp { get; set; }

    [JsonPropertyName("destination")]
    public string Destination { get; set; } = default!;

    [JsonPropertyName("content")]
    public JsonElement? Content { get; set; }

    [JsonPropertyName("server_keys")]
    public ServerKeys ServerKeys { get; set; } = default!;

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
