using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

[EventType(EventTypes.CanonicalAlias)]
public class CanonicalAliasEvent
{
    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("alt_aliases")]
    public string[]? AlternativeAliases { get; set; }
}
