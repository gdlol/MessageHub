using System.Text.Json.Serialization;
using MessageHub.HomeServer.Events.General;

namespace MessageHub.ClientServer.Protocol;

public class SyncRequest
{
    [JsonPropertyName("filter")]
    public string? Filter { get; set; }

    [JsonPropertyName("full_state")]
    public bool FullState { get; set; } = false;

    [JsonPropertyName("set_presence")]
    public string SetPresence { get; set; } = PresenceValues.Online;

    [JsonPropertyName("since")]
    public string? Since { get; set; }

    [JsonPropertyName("timeout")]
    public long Timeout { get; set; } = 0;
}
