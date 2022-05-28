using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class LogInParameters
{
    [Required]
    [JsonPropertyName("type")]
    public string LogInType { get; set; } = default!;

    [Required]
    [JsonPropertyName("token")]
    public string Token { get; set; } = default!;

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("initial_device_display_name")]
    public string? InitialDeviceDisplayName { get; set; }
}
