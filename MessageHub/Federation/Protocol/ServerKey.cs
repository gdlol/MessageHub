using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;

namespace MessageHub.Federation.Protocol;

public class OldVerifyKey
{
    [Required]
    [JsonPropertyName("expired_ts")]
    public long ExpiredTimestamp { get; set; }

    [Required]
    [JsonPropertyName("key")]
    public string Key { get; set; } = default!;
}

public class ServerKey
{
    [JsonPropertyName("old_verify_keys")]
    public Dictionary<string, OldVerifyKey> OldVerifyKeys { get; init; } = default!;

    [JsonPropertyName("server_name")]
    public string ServerName { get; init; } = default!;

    [JsonPropertyName("signatures")]
    public Signatures Signatures { get; init; } = default!;

    [JsonPropertyName("valid_until_ts")]
    public long ValidUntilTimestamp { get; init; }

    [JsonPropertyName("verify_keys")]
    public Dictionary<KeyIdentifier, string> VerifyKeys { get; init; } = default!;
}
