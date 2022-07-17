using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class SetVisibilityRequest
{
    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = default!;
}
