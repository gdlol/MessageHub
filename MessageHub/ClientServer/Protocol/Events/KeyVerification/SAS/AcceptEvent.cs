using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events.KeyVerification.SAS;

[EventType(EventTypes.Accept)]
public class AcceptEvent
{
    [Required]
    [JsonPropertyName("commitment")]
    public string Commitment { get; set; } = default!;

    [Required]
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = default!;

    [Required]
    [JsonPropertyName("key_agreement_protocol")]
    public string KeyAgreementProtocol { get; set; } = default!;

    [JsonPropertyName("m.relates_to")]
    public VerificationRelatesTo? RelatesTo { get; set; }

    [Required]
    [JsonPropertyName("method")]
    public string Method { get; set; } = default!;

    [Required]
    [JsonPropertyName("message_authentication_code")]
    public string MessageAuthenticationCode { get; set; } = default!;

    [Required]
    [JsonPropertyName("short_authentication_string")]
    public string[] ShortAuthenticationString { get; set; } = default!;

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }
}
