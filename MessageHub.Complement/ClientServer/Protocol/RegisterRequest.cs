using System.Text.Json.Serialization;

namespace MessageHub.Complement.ClientServer.Protocol;

public class RegisterRequest
{
    [JsonPropertyName("auth")]
    public AuthenticationData? AuthenticationData { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("inhibit_login")]
    public bool? InhibitLogin { get; set; }

    [JsonPropertyName("initial_device_display_name")]
    public string? InitialDeviceDisplayName { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("refresh_token")]
    public bool? RefreshToken { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}
