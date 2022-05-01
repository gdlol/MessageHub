using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

[EventType(EventTypes.Avatar)]
public class AvatarEvent
{
    [JsonPropertyName("info")]
    public JsonElement? Info { get; init; }

    [Required]
    [JsonPropertyName("url")]
    public string Url { get; init; } = default!;
}
