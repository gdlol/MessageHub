using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events.Room;

[EventType(EventTypes.Avatar)]
public class AvatarEvent
{
    [JsonPropertyName("info")]
    public ImageInfo? Info { get; init; }

    [Required]
    [JsonPropertyName("url")]
    public string Url { get; init; } = default!;
}
