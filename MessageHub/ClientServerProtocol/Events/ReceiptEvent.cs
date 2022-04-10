using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events;

public class Receipt
{
    [JsonPropertyName("ts")]
    public string? Timestamp { get; set; }
}

public class Receipts
{
    [JsonPropertyName("m.read")]
    public Dictionary<string, Receipt>? ReadReceipts { get; set; }
}

[EventType(EventType)]
public class ReceiptEvent : Dictionary<string, Receipts>
{
    public const string EventType = "m.receipt";
}
