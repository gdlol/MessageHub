using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.KeyVerification;

public class VerificationRelatesTo
{
    [JsonPropertyName("event_id")]
    public string? EventId { get; set; }

    [JsonPropertyName("rel_type")]
    public string? RelationshipType { get; set; }
}
