using System.Text.Json.Serialization;
using MessageHub.HomeServer.Events;

namespace MessageHub.Federation.Protocol;

public class PushMessagesRequest
{
    [JsonPropertyName("edus")]
    public EphemeralDataUnit[]? Edus { get; set; }

    [JsonPropertyName("origin")]
    public string Origin { get; set; } = default!;

    [JsonPropertyName("origin_server_ts")]
    public long OriginServerTimestamp { get; set; }

    [JsonPropertyName("pdus")]
    public PersistentDataUnit[] Pdus { get; set; } = default!;
}
