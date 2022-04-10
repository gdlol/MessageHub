using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events;

public class Tag
{
    [JsonPropertyName("order")]
    public double? Order { get; set; }
}

[EventType(EventType)]
public class TagEvent
{
    public const string EventType = "m.tag";

    [JsonPropertyName("user_ids")]
    public Dictionary<string, Tag>? Tags { get; set; }
}
