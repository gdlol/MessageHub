using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class UploadResponse
{
    [JsonPropertyName("content_uri")]
    public string ContentUrl { get; set; } = default!;
}
