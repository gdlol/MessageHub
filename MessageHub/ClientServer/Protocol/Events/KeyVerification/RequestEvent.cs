using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events.KeyVerification;

[EventType(EventTypes.Request)]
public class RequestEvent
{
    [Required]
    [JsonPropertyName("from_device")]
    public string FromDevice { get; set; } = default!;

    [Required]
    [JsonPropertyName("methods")]
    public string[] Methods { get; set; } = default!;

    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; set; }

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }
}
