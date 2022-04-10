using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Call;

public class Candidate
{
    [Required]
    [JsonPropertyName("candidate")]
    public string CandidateLine { get; set; } = default!;

    [Required]
    [JsonPropertyName("sdpMLineIndex")]
    public string SdpMLineIndex { get; set; } = default!;

    [Required]
    [JsonPropertyName("sdpMid")]
    public string SdpMid { get; set; } = default!;
}

[EventType(EventTypes.Candidates)]
public class CandidatesEvent
{
    [Required]
    [JsonPropertyName("call_id")]
    public string CallId { get; set; } = default!;

    [Required]
    [JsonPropertyName("candidates")]
    public Candidate[] Candidates { get; set; } = default!;

    [Required]
    [JsonPropertyName("version")]
    public string Version { get; set; } = default!;
}
