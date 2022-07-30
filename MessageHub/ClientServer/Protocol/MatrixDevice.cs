using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public record class MatrixDevice
{
    [JsonPropertyName("device_id")]
    public string DeviceId { get; init; } = default!;

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("last_seen_ip")]
    public string? LastSeenIP { get; init; }

    [JsonPropertyName("last_seen_ts")]
    public long? LastSeenTimestamp { get; init; }
}
