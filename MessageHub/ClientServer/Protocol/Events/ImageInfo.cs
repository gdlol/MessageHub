using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events;

public class ImageInfo
{
    [JsonPropertyName("h")]
    public int? Height { get; init; }

    [JsonPropertyName("mimetype")]
    public string? MimeType { get; init; }

    [JsonPropertyName("size")]
    public int? Size { get; init; }

    [JsonPropertyName("thumbnail_file")]
    public EncryptedFile? ThumbnailFile { get; init; }

    [JsonPropertyName("thumbnail_info")]
    public ThumbnailInfo? ThumbnailInfo { get; init; }

    [JsonPropertyName("thumbnail_url")]
    public string? ThumbnailUrl { get; init; }

    [JsonPropertyName("w")]
    public int? Width { get; init; }
}
