using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Call;

[EventType(CallEventTypes.Hangup)]
public class HangupEvent
{
    [Required]
    [JsonPropertyName("call_id")]
    public string CallId { get; set; } = default!;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [Required]
    [JsonPropertyName("version")]
    public string Version { get; set; } = default!;
}
