using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

public static class HistoryVisibilityKinds
{
    public const string WorldReadable = "world_readable";
    public const string Shared = "shared";
    public const string Invited = "invited";
    public const string Joined = "joined";
}

[EventType(EventTypes.HistoryVisibility)]
public class HistoryVisibilityEvent
{
    [Required]
    [JsonPropertyName("history_visibility")]
    public string HistoryVisibility { get; init; } = default!;
}
