using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class SetDeviceRequest
{
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}
