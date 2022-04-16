using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events.Room;

[EventType(EventTypes.Message)]
public class ServerNoticeMessageEvent
{
    [Required]
    [JsonPropertyName("admin_contact")]
    public string? AdminContact { get; set; }

    [Required]
    [JsonPropertyName("body")]
    public string Body { get; set; } = default!;

    [JsonPropertyName("limit_type")]
    public string? LimitType { get; set; }

    [Required]
    [JsonPropertyName("msgtype")]
    public string MessageType { get; set; } = "m.server_notice";

    [Required]
    [JsonPropertyName("server_notice_type")]
    public string ServerNoticeType { get; set; } = default!;
}
