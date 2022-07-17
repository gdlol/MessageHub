using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.Room;

public static class MembershipStates
{
    public const string Invite = "invite";
    public const string Join = "join";
    public const string Knock = "knock";
    public const string Leave = "leave";
    public const string Ban = "ban";
}

[EventType(EventTypes.Member)]
public record MemberEvent
{
    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }

    [JsonPropertyName("displayname")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("is_direct")]
    public bool? IsDirect { get; init; }

    [JsonPropertyName("membership")]
    public string MemberShip { get; init; } = default!;

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
