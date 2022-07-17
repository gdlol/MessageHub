using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MessageHub.HomeServer.Events;

namespace MessageHub.Federation.Protocol;

public class InviteRequest
{
    [Required]
    [JsonPropertyName("event")]
    public PersistentDataUnit Event { get; set; } = default!;

    [JsonPropertyName("invite_room_state")]
    public StrippedStateEvent[]? InviteRoomState { get; set; }

    [Required]
    [JsonPropertyName("room_version")]
    public int RoomVersion { get; set; }
}
