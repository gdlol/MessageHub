using System.Text.Json.Serialization;

namespace MessageHub.Complement.ClientServer.Protocol;

public class AuthenticationData
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("identifier")]
    public LogInUserIdentifier? Identifier { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("session")]
    public string? Session { get; set; }
}
