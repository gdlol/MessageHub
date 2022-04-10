using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

[EventType(EventTypes.Avatar)]
public class AvatarEvent
{
    [JsonPropertyName("info")]
    public ImageInfo? Info { get; set; }

    [Required]
    [JsonPropertyName("url")]
    public string Url { get; set; } = default!;
}
