using System.Text.Json.Serialization;

namespace MessageHub.ElementServer;

public class Config
{
    [JsonPropertyName("listenAddress")]
    public string ListenAddress { get; set; } = default!;

    [JsonPropertyName("element.listenAddress")]
    public string? ElementListenAddress { get; set; }
}
