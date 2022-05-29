using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.P2p;

public class Config
{
    [JsonPropertyName("peerId")]
    public string PeerId { get; set; } = default!;

    [JsonPropertyName("listenAddress")]
    public string ListenAddress { get; set; } = default!;

    [JsonPropertyName("contentPath")]
    public string ContentPath { get; set; } = default!;

    [JsonPropertyName("dataPath")]
    public string DataPath { get; set; } = default!;
}
