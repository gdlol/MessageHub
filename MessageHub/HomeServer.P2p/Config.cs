using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.P2p;

public class Config
{
    [JsonPropertyName("peerId")]
    public string PeerId { get; set; } = default!;

    [JsonPropertyName("peers")]
    public Dictionary<string, string> Peers { get; set; } = default!;

    [JsonPropertyName("contentPath")]
    public string ContentPath { get; set; } = default!;

    [JsonPropertyName("dataPath")]
    public string DataPath { get; set; } = default!;
}
