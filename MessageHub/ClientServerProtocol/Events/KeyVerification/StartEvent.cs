using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.KeyVerification;

[EventType(EventTypes.Start)]
public class StartEvent
{
    [Required]
    [JsonPropertyName("from_device")]
    public string FromDevice { get; set; } = default!;

    [JsonPropertyName("m.relates_to")]
    public VerificationRelatesTo? RelatesTo { get; set; }

    [Required]
    [JsonPropertyName("method")]
    public string Method { get; set; } = default!;

    [JsonPropertyName("next_method")]
    public string? NextMethod { get; set; } = default!;

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }
}
