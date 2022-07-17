using System.Text.Json.Serialization;

namespace MessageHub.Federation.Protocol;

public class GetMissingEventsRequest
{
    [JsonPropertyName("earliest_events")]
    public string[] EarliestEvents { get; set; } = default!;

    [JsonPropertyName("latest_events")]
    public string[] LatestEvents { get; set; } = default!;

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("min_depth")]
    public int MinDepth { get; set; }
}
