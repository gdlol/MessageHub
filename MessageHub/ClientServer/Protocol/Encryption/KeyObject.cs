using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Encryption;

public class KeyObject
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = default!;

    [JsonPropertyName("signatures")]
    public Dictionary<string, Dictionary<string, string>> Signatures { get; set; } = default!;
}
