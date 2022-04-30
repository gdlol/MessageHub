using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

public static class JoinRules
{
    public const string Public = "public";
    public const string Knock = "knock";
    public const string Invite = "invite";
    public const string Private = "private";
}

[EventType(EventTypes.JoinRules)]
public class JoinRulesEvent
{
    [Required]
    [JsonPropertyName("join_rule")]
    public string JoinRule { get; init; } = default!;
}
