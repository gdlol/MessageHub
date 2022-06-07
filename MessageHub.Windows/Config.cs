using System.Text.Json.Serialization;

namespace MessageHub.Windows;

public class Config
{
    [JsonPropertyName("client.listenAddress")]
    public string? ClientListenAddress { get; set; }

    [JsonPropertyName("client.launchOnStart")]
    public bool LaunchClientOnStart { get; set; } = true;
}
