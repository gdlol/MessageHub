using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.KeyVerification.SAS;

[EventType(EventTypes.Key)]
public class KeyEvent
{
    [Required]
    [JsonPropertyName("key")]
    public string Key { get; set; } = default!;

    [JsonPropertyName("m.relates_to")]
    public VerificationRelatesTo? RelatesTo { get; set; }

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }
}
