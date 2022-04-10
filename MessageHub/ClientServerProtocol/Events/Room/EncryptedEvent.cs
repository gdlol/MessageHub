using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

[EventType(EventTypes.Encrypted)]
public class EncryptedEvent
{
    [Required]
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = default!;

    [Required]
    [JsonPropertyName("ciphertext")]
    public JsonElement CipherText { get; set; } = default!;

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [Required]
    [JsonPropertyName("sender_key")]
    public string SenderKey { get; set; } = default!;

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}
