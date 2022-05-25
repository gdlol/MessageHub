using System.Text.Json.Serialization;
using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer;

public class ServerKeys
{
    [JsonPropertyName("server_name")]
    public string ServerName { get; init; } = default!;

    [JsonPropertyName("signatures")]
    public Signatures Signatures { get; init; } = default!;

    [JsonPropertyName("valid_until_ts")]
    public long ValidUntilTimestamp { get; init; }

    [JsonPropertyName("verify_keys")]
    public Dictionary<KeyIdentifier, string> VerifyKeys { get; init; } = default!;
}
