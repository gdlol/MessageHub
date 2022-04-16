using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events;

[EventType(EventType)]
public class PresenceEvent
{
    public const string EventType = "m.presence";

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("currently_active")]
    public bool? CurrentlyActive { get; set; }

    [JsonPropertyName("displayname")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("last_active_ago")]
    public long? LastActiveAgo { get; set; }

    [Required]
    [JsonPropertyName("presence")]
    public string Presence { get; set; } = default!;

    [JsonPropertyName("status_msg")]
    public string? StatusMessage { get; set; }
}
