using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class SetReadMarkerRequest
{
    [JsonPropertyName("m.fully_read")]
    public string FullyRead { get; set; } = default!;

    [JsonPropertyName("m.read")]
    public string? Read { get; set; }
}
