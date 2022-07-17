using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class SetAliasRequest
{
    [Required]
    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = default!;
}
