using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class SetVisibilityRequest
{
    [Required]
    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = default!;
}
