using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol;

public class SetVisibilityParameters
{
    [Required]
    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = default!;
}
