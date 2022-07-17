using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class SetAvatarUrlRequest
{
    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; } = default!;
}
