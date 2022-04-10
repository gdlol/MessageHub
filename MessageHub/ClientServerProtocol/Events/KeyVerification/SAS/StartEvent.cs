using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.KeyVerification.SAS;

[EventType(EventTypes.Start)]
public class StartEvent
{
    [Required]
    [JsonPropertyName("from_device")]
    public string FromDevice { get; set; } = default!;

    [Required]
    [JsonPropertyName("hashes")]
    public string[] Hashes { get; set; } = default!;

    [Required]
    [JsonPropertyName("key_agreement_protocols")]
    public string[] KeyAgreementProtocols { get; set; } = default!;

    [JsonPropertyName("m.relates_to")]
    public VerificationRelatesTo? RelatesTo { get; set; }

    [Required]
    [JsonPropertyName("message_authentication_codes")]
    public string[] MessageAuthenticationCodes { get; set; } = default!;

    [Required]
    [JsonPropertyName("method")]
    public string Method { get; set; } = default!;

    [Required]
    [JsonPropertyName("short_authentication_string")]
    public string[] ShortAuthenticationString { get; set; } = default!;

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }
}
