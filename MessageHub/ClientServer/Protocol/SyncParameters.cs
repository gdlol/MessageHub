using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class SyncParameters
{
    public static class SetPresenceValues
    {
        public const string Offline = "offline";
        public const string Online = "online";
        public const string Unavailable = "unavailable";
    }

    [JsonPropertyName("filter")]
    public string? Filter { get; set; }

    [JsonPropertyName("full_state")]
    public bool FullState { get; set; } = false;

    [JsonPropertyName("set_presence")]
    public string SetPresence { get; set; } = SetPresenceValues.Online;

    [JsonPropertyName("since")]
    public string Since { get; set; } = string.Empty;

    [JsonPropertyName("timeout")]
    public long Timeout { get; set; } = 0;
}
