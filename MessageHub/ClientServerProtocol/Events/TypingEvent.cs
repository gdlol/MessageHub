using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events;

[EventType(EventType)]
public class TypingEvent
{
    public const string EventType = "m.typing";

    [Required]
    [JsonPropertyName("user_ids")]
    public string[] UserIds { get; set; } = default!;
}
