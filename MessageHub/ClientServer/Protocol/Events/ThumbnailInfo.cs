using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events;

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
