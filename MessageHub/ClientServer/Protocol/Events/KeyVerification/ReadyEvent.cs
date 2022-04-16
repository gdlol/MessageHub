using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events.KeyVerification;

[EventType(EventTypes.Ready)]
public class ReadyEvent
{
    [Required]
    [JsonPropertyName("from_device")]
    public string FromDevice { get; set; } = default!;

    [JsonPropertyName("m.relates_to")]
    public VerificationRelatesTo? RelatesTo { get; set; }

    [Required]
    [JsonPropertyName("methods")]
    public string[] Methods { get; set; } = default!;

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }
}
