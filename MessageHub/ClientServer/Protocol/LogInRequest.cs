using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class LogInRequest
{
    [JsonPropertyName("type")]
    public string LogInType { get; set; } = default!;

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("initial_device_display_name")]
    public string? InitialDeviceDisplayName { get; set; }
}
