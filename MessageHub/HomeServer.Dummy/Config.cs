using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Dummy;

public class Config
{
    [JsonPropertyName("peerId")]
    public string PeerId { get; set; } = default!;

    [JsonPropertyName("peers")]
    public Dictionary<string, string> Peers { get; set; } = default!;
}
