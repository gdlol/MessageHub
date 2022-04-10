using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol.Events.Room;

public static class MembershipStates
{
    public const string Invite = "invite";
    public const string Join = "join";
    public const string Knock = "knock";
    public const string Leave = "leave";
    public const string Ban = "ban";
}

public class Signed
{
    [Required]
    [JsonPropertyName("mxid")]
    public string MatrixId { get; set; } = default!;

    [Required]
    [JsonPropertyName("signatures")]
    public JsonElement Signatures { get; set; } = default!;

    [Required]
    [JsonPropertyName("token")]
    public string Token { get; set; } = default!;
}

public class Invite
{
    [Required]
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = default!;

    [Required]
    [JsonPropertyName("signed")]
    public Signed Signed { get; set; } = default!;
}

[EventType(EventTypes.Member)]
public class MemberEvent
{

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("displayname")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("is_direct")]
    public bool? IsDirect { get; set; }

    [JsonPropertyName("join_authorised_via_users_server")]
    public string? JoinAuthorizingServer { get; set; }

    [Required]
    [JsonPropertyName("membership")]
    public string MemberShip { get; set; } = default!;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("third_party_invite")]
    public Invite? ThirdPartyInvite { get; set; }
}
