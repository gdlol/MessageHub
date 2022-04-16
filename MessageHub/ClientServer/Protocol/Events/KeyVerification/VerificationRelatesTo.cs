using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events.KeyVerification;

public class VerificationRelatesTo
{
    [JsonPropertyName("event_id")]
    public string? EventId { get; set; }

    [JsonPropertyName("rel_type")]
    public string? RelationshipType { get; set; }
}
