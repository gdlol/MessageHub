using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

public static class JoinRules
{
    public const string Public = "public";
    public const string Knock = "knock";
    public const string Invite = "invite";
    public const string Private = "private";
    public const string Restricted = "restricted";
}

public class AllowCondition
{
    [JsonPropertyName("room_id")]
    public string? RoomId { get; set; }

    [Required]
    [JsonPropertyName("type")]
    public string ConditionType { get; set; } = "m.room_membership";
}

[EventType(EventTypes.JoinRules)]
public class JoinRulesEvent
{

    [JsonPropertyName("allow")]
    public AllowCondition[]? Allow { get; set; }

    [Required]
    [JsonPropertyName("join_rule")]
    public string JoinRule { get; set; } = default!;
}
