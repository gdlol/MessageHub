using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events.KeyVerification.Reciprocate;

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

    [Required]
    [JsonPropertyName("secret")]
    public string Secret { get; set; } = default!;

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }
}
