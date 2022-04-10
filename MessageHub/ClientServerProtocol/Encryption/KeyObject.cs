using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Encryption;

public class KeyObject
{
    [Required]
    [JsonPropertyName("key")]
    public string Key { get; set; } = default!;

    [Required]
    [JsonPropertyName("signatures")]
    public Dictionary<string, Dictionary<string, string>> Signatures { get; set; } = default!;
}
