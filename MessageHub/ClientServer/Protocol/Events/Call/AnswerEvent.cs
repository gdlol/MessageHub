using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol.Events.Call;

public class Answer
{
    [Required]
    [JsonPropertyName("sdp")]
    public string SDP { get; set; } = default!;

    [Required]
    [JsonPropertyName("type")]
    public string AnswerType { get; set; } = default!;
}

[EventType(EventTypes.Answer)]
public class AnswerEvent
{
    [Required]
    [JsonPropertyName("answer")]
    public Answer Answer { get; set; } = default!;

    [Required]
    [JsonPropertyName("call_id")]
    public string CallId { get; set; } = default!;

    [Required]
    [JsonPropertyName("version")]
    public string Version { get; set; } = default!;
}
