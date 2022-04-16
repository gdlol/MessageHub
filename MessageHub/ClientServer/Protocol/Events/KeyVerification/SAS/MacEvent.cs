using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events.KeyVerification.SAS;

[EventType(EventTypes.Mac)]
public class MacEvent
{
    [Required]
    [JsonPropertyName("keys")]
    public string Keys { get; set; } = default!;

    [JsonPropertyName("m.relates_to")]
    public VerificationRelatesTo? RelatesTo { get; set; }

    [Required]
    [JsonPropertyName("mac")]
    public Dictionary<string, string> Mac { get; set; } = default!;

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }
}
