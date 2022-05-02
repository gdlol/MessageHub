using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MessageHub.HomeServer.Events;

namespace MessageHub.Federation.Protocol;

public class PushMessagesRequest
{
    [JsonPropertyName("edus")]
    public EphemeralDataUnit[]? Edus { get; set; }

    [Required]
    [JsonPropertyName("origin")]
    public string Origin { get; set; } = default!;

    [Required]
    [JsonPropertyName("origin_server_ts")]
    public long OriginServerTimestamp { get; set; }

    [Required]
    [JsonPropertyName("pdus")]
    public PersistentDataUnit[] Pdus { get; set; } = default!;
}
