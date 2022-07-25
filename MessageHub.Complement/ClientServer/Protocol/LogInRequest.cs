using System.Text.Json.Serialization;

namespace MessageHub.Complement.ClientServer.Protocol;

public class LogInUserIdentifier
{
    [JsonPropertyName("type")]
    public string IdentifierType { get; set; } = default!;

    [JsonPropertyName("user")]
    public string User { get; set; } = default!;
}

public class LogInRequest
{
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("identifier")]
    public LogInUserIdentifier? Identifier { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("type")]
    public string LogInType { get; set; } = default!;
}
