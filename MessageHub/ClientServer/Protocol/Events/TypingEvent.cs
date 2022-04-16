using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events;

[EventType(EventType)]
public class TypingEvent
{
    public const string EventType = "m.typing";

    [Required]
    [JsonPropertyName("user_ids")]
    public string[] UserIds { get; set; } = default!;
}
