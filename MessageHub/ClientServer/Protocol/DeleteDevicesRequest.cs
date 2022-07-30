using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class DeleteDevicesRequest
{
    [JsonPropertyName("devices")]
    public string[] Devices { get; set; } = default!;
}
