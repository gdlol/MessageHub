using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class GetPresenceResponse
{
    [JsonPropertyName("currently_active")]
    public bool? CurrentlyActive { get; init; }

    [JsonPropertyName("last_active_ago")]
    public long? LastActiveAgo { get; init; }

    [JsonPropertyName("presence")]
    public string Presence { get; init; } = default!;

    [JsonPropertyName("status_msg")]
    public string? StatusMessage { get; init; }
}
