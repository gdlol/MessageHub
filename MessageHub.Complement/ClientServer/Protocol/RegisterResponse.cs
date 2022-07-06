using System.Text.Json.Serialization;

namespace MessageHub.Complement.ClientServer.Protocol;

public class RegisterResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("expires_in_ms")]
    public long? ExpiresInMillisecond { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = default!;
}
