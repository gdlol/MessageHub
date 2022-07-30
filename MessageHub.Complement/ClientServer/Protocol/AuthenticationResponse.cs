using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.Complement.ClientServer.Protocol;

public class FlowInformation
{
    [JsonPropertyName("stages")]
    public string[] Stages { get; set; } = default!;
}

public class AuthenticationResponse
{
    [JsonPropertyName("errcode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("completed")]
    public string[]? Completed { get; set; }

    [JsonPropertyName("flows")]
    public FlowInformation[] Flows { get; set; } = default!;

    [JsonPropertyName("params")]
    public JsonElement? Parameters { get; set; }

    [JsonPropertyName("session")]
    public string? Session { get; set; }
}
