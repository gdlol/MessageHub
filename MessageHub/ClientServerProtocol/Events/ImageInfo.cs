using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events;

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
