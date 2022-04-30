using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

[EventType(EventTypes.Message)]
public class MessageEvent
{
    [Required]
    [JsonPropertyName("body")]
    public string Body { get; set; } = default!;

    [Required]
    [JsonPropertyName("msgtype")]
    public string MessageType { get; set; } = default!;
}
