using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class SearchUserResponse
{
    public class User
    {
        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }
    }

    [JsonPropertyName("limited")]
    public bool Limited { get; set; }

    [JsonPropertyName("results")]
    public User[] Results { get; set; } = default!;
}
