using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.General;

[EventType(EventType)]
public class FullyReadEvent
{
    public const string EventType = "m.fully_read";

    [Required]
    [JsonPropertyName("event_id")]
    public string EventId { get; init; } = default!;
}
