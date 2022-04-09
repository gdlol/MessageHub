using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

[EventType(RoomEventTypes.CanonicalAlias)]
public class CanonicalAliasEvent
{
    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("alt_aliases")]
    public List<string>? AlternativeAliases { get; set; }
}
