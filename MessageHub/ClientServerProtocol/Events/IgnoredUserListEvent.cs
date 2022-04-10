using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events;

[EventType(EventType)]
public class IgnoredUserListEvent
{
    public const string EventType = "m.ignored_user_list";

    [Required]
    [JsonPropertyName("ignored_users")]
    public Dictionary<string, JsonElement> IgnoredUsers { get; set; } = default!;
}
