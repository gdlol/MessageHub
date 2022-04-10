using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.KeyVerification;

[EventType(EventTypes.Done)]
public class DoneEvent
{
    [JsonPropertyName("m.relates_to")]
    public VerificationRelatesTo? RelatesTo { get; set; }

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }
}
