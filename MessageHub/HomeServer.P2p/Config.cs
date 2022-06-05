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

    [JsonPropertyName("libp2p.port")]
    public int P2pPort { get; set; }

    [JsonPropertyName("libp2p.staticRelays")]
    public string[]? StaticRelays { get; set; }

    [JsonPropertyName("libp2p.privateNetworkSecret")]
    public string? PrivateNetworkSecret { get; set; }

    [JsonPropertyName("libp2p.dht.bootstrapPeers")]
    public string[]? BootstrapPeers { get; set; }
}
