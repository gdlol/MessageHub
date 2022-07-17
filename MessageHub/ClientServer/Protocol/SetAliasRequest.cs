using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class SetAliasRequest
{
    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = default!;
}
