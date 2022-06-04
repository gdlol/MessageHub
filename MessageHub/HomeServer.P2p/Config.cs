using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.P2p;

public class Config
{
    [JsonPropertyName("listenAddress")]
    public string ListenAddress { get; set; } = default!;

    [JsonPropertyName("contentPath")]
    public string ContentPath { get; set; } = default!;

    [JsonPropertyName("dataPath")]
    public string DataPath { get; set; } = default!;
}
