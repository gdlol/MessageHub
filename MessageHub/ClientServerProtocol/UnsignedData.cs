using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol;

public class UnsignedData
{
    [JsonPropertyName("age")]
    public long? Age { get; set; }

    [JsonPropertyName("prev_content")]
    public object? PreviousContent { get; set; }

    [JsonPropertyName("redacted_because")]
    public ClientEvent? RedactedBecause { get; set; }

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }
}
