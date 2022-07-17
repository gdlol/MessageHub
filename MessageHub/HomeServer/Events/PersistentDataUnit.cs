using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Serialization;

namespace MessageHub.HomeServer.Events;

public class ServerSignatures : Dictionary<KeyIdentifier, string> { }

public class Signatures : Dictionary<string, ServerSignatures> { }

public class PersistentDataUnit
{
    [JsonPropertyName("auth_events")]
    public string[] AuthorizationEvents { get; set; } = default!;

    [JsonPropertyName("content")]
    public JsonElement Content { get; set; } = default!;

    [JsonPropertyName("depth")]
    public long Depth { get; set; } = default!;

    [JsonPropertyName("hashes")]
    public Dictionary<string, string> Hashes { get; set; } = default!;

    [JsonPropertyName("origin_server_ts")]
    public long OriginServerTimestamp { get; set; }

    [JsonPropertyName("prev_events")]
    public string[] PreviousEvents { get; set; } = default!;

    [JsonPropertyName("redacts")]
    public string? Redacts { get; set; }

    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = default!;

    [JsonPropertyName("sender")]
    public string Sender { get; set; } = default!;

    [JsonPropertyName("server_keys")]
    public ServerKeys ServerKeys { get; set; } = default!;

    [JsonPropertyName("signatures")]
    public Signatures Signatures { get; set; } = default!;

    [JsonPropertyName("state_key")]
    public string? StateKey { get; set; }

    [JsonPropertyName("type")]
    public string EventType { get; set; } = default!;

    [JsonPropertyName("unsigned")]
    public JsonElement? Unsigned { get; set; }

    public JsonElement ToJsonElement() => DefaultJsonSerializer.SerializeToElement(this);

    public override string ToString()
    {
        return ToJsonElement().ToString();
    }
}
