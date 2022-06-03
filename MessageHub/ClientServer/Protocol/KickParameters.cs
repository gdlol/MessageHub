using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class KickParameters
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; } 

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = default!;
}
