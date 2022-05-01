using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

[EventType(EventTypes.Message)]
public class ServerNoticeMessageEvent
{
    [Required]
    [JsonPropertyName("admin_contact")]
    public string? AdminContact { get; init; }

    [Required]
    [JsonPropertyName("body")]
    public string Body { get; init; } = default!;

    [JsonPropertyName("limit_type")]
    public string? LimitType { get; init; }

    [Required]
    [JsonPropertyName("msgtype")]
    public string MessageType { get; init; } = "m.server_notice";

    [Required]
    [JsonPropertyName("server_notice_type")]
    public string ServerNoticeType { get; init; } = default!;
}
