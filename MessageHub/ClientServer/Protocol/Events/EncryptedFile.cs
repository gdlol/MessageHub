using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events;

public class EncryptedFile
{
    [Required]
    [JsonPropertyName("url")]
    public string Url { get; init; } = default!;

    [Required]
    [JsonPropertyName("key")]
    public JsonElement Key { get; init; } = default!;

    [Required]
    [JsonPropertyName("iv")]
    public string IV { get; init; } = default!;

    [Required]
    [JsonPropertyName("hashes")]
    public Dictionary<string, string> Hashes { get; init; } = default!;

    [Required]
    [JsonPropertyName("v")]
    public string Version { get; init; } = default!;
}
