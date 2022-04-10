using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

[EventType(EventTypes.CanonicalAlias)]
public class CanonicalAliasEvent
{
    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("alt_aliases")]
    public string[]? AlternativeAliases { get; set; }
}
