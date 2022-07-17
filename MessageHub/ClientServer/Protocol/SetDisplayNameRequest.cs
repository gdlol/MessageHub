using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class SetDisplayNameRequest
{
    [JsonPropertyName("displayname")]
    public string DisplayName { get; set; } = default!;
}
