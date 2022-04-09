using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events;

public class EncryptedFile
{
    [Required]
    [JsonPropertyName("url")]
    public string Url { get; set; } = default!;

    [Required]
    [JsonPropertyName("key")]
    public JsonElement Key { get; set; } = default!;

    [Required]
    [JsonPropertyName("iv")]
    public string IV { get; set; } = default!;

    [Required]
    [JsonPropertyName("hashes")]
    public Dictionary<string, string> Hashes { get; set; } = default!;

    [Required]
    [JsonPropertyName("v")]
    public string Version { get; set; } = default!;
}
