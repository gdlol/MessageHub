using System.Text.Json.Serialization;

namespace MessageHub.Complement.ClientServer.Protocol;

public class DeleteDevicesRequest
{
    [JsonPropertyName("auth")]
    public AuthenticationData? AuthenticationData { get; set; }

    [JsonPropertyName("devices")]
    public string[] Devices { get; set; } = default!;
}
