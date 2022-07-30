using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public record class WhoAmIResponse
{
    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = default!;

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; init; }
}
