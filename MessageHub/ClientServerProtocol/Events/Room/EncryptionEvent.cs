using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

[EventType(EventTypes.Encryption)]
public class EncryptionEvent
{
    [Required]
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = default!;

    [JsonPropertyName("rotation_period_ms")]
    public long? RotationPeriodMilliseconds { get; set; }

    [JsonPropertyName("rotation_period_msgs")]
    public long? RotationPeriodMessages { get; set; }
}
