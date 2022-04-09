using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

public class ThumbnailInfo
{
    [JsonPropertyName("h")]
    public int? Height { get; set; }

    [JsonPropertyName("mimetype")]
    public string? MimeType { get; set; }

    [JsonPropertyName("size")]
    public int? Size { get; set; }

    [JsonPropertyName("w")]
    public int? Width { get; set; }
}

public class ImageInfo
{
    [JsonPropertyName("h")]
    public int? Height { get; set; }

    [JsonPropertyName("mimetype")]
    public string? MimeType { get; set; }

    [JsonPropertyName("size")]
    public int? Size { get; set; }

    [JsonPropertyName("thumbnail_file")]
    public EncryptedFile? ThumbnailFile { get; set; }

    [JsonPropertyName("thumbnail_info")]
    public ThumbnailInfo? ThumbnailInfo { get; set; }

    [JsonPropertyName("thumbnail_url")]
    public string? ThumbnailUrl { get; set; }

    [JsonPropertyName("w")]
    public int? Width { get; set; }
}

[EventType(RoomEventTypes.Avatar)]
public class AvatarEvent
{
    [JsonPropertyName("info")]
    public ImageInfo? Info { get; set; }

    [Required]
    [JsonPropertyName("url")]
    public string Url { get; set; } = default!;
}
