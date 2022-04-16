using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events;

[EventType(EventType)]
public class StickerEvent
{
    public const string EventType = "m.sticker";

    [Required]
    [JsonPropertyName("body")]
    public string Body { get; set; } = default!;

    [Required]
    [JsonPropertyName("info")]
    public ImageInfo Info { get; set; } = default!;

    [Required]
    [JsonPropertyName("url")]
    public string Url { get; set; } = default!;
}
