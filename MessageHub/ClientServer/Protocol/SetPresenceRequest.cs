using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class SetPresenceRequest
{
    [JsonPropertyName("presence")]
    public string Presence { get; init; } = default!;

    [JsonPropertyName("status_msg")]
    public string? StatusMessage { get; init; }
}
