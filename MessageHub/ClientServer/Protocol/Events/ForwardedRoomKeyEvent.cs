using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events;

[EventType(EventType)]
public class ForwardedRoomKeyEvent
{
    public const string EventType = "m.forwarded_room_key";

    [Required]
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = default!;

    [Required]
    [JsonPropertyName("forwarding_curve25519_key_chain")]
    public string[] ForwardingCurve25519KeyChain { get; set; } = default!;

    [Required]
    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = default!;

    [Required]
    [JsonPropertyName("sender_claimed_ed25519_key")]
    public string SenderClaimedEd25519Key { get; set; } = default!;

    [Required]
    [JsonPropertyName("sender_key")]
    public string SenderKey { get; set; } = default!;

    [Required]
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = default!;

    [Required]
    [JsonPropertyName("session_key")]
    public string SessionKey { get; set; } = default!;

    [JsonPropertyName("withheld")]
    public JsonElement Withheld { get; set; } = default!;
}
