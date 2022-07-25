using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public record class LogInResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; init; }

    [JsonPropertyName("expires_in_ms")]
    public long? ExpiresInMillisecond { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = default!;
}
