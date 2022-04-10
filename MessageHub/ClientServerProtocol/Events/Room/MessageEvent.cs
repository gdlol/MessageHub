using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

[EventType(EventTypes.Message)]
public class MessageEvent
{
    [Required]
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("msgtype")]
    public string MessageType { get; set; } = string.Empty;
}
