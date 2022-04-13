using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol;

public class RoomMember
{
    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; } = default!;

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; } = default!;
}
