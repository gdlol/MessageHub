using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Call;

public class Offer
{
    [Required]
    [JsonPropertyName("sdp")]
    public string SDP { get; set; } = default!;

    [Required]
    [JsonPropertyName("type")]
    public string AnswerType { get; set; } = default!;
}

[EventType(CallEventTypes.Invite)]
public class InviteEvent
{
    [Required]
    [JsonPropertyName("call_id")]
    public string CallId { get; set; } = default!;

    [Required]
    [JsonPropertyName("lifetime")]
    public long Lifetime { get; set; } = default!;

    [Required]
    [JsonPropertyName("offer")]
    public Offer Offer { get; set; } = default!;

    [Required]
    [JsonPropertyName("version")]
    public string Version { get; set; } = default!;
}
