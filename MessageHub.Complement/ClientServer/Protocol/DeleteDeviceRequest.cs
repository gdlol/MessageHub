using System.Text.Json.Serialization;

namespace MessageHub.Complement.ClientServer.Protocol;

public class DeleteDeviceRequest
{
    [JsonPropertyName("auth")]
    public AuthenticationData? AuthenticationData { get; set; }
}
