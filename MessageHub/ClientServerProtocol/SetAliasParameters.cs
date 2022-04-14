using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol;

public class SetAliasParameters
{
    [Required]
    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = default!;
}
