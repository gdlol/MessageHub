using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.HomeServer.Events;

namespace MessageHub.ClientServer.Protocol;

public class InviteState
{
    [JsonPropertyName("events")]
    public StrippedStateEvent[]? Events { get; set; }
}

public class Event
{
    [JsonPropertyName("content")]
    public JsonElement? Content { get; set; }

    [JsonPropertyName("sender")]
    public string? Sender { get; set; }

    [Required]
    [JsonPropertyName("type")]
    public string EventType { get; set; } = default!;
}

public class AccountData
{
    [JsonPropertyName("events")]
    public Event[]? Events { get; set; }
}

public class Ephemeral
{
    [JsonPropertyName("events")]
    public Event[]? Events { get; set; }
}

public class State
{
    [JsonPropertyName("events")]
    public ClientEventWithoutRoomID[]? Events { get; set; }
}

public class RoomSummary
{
    [JsonPropertyName("m.heroes")]
    public string[]? Heroes { get; set; }

    [JsonPropertyName("m.invited_member_count")]
    public int? InvitedMemberCount { get; set; }

    [JsonPropertyName("m.joined_member_count")]
    public int? JoinedMemberCount { get; set; }
}

public class Timeline
{
    [Required]
    [JsonPropertyName("events")]
    public ClientEventWithoutRoomID[] Events { get; set; } = default!;

    [JsonPropertyName("limited")]
    public bool? Limited { get; set; }

    [JsonPropertyName("prev_batch")]
    public string? PreviousBatch { get; set; }
}

public class UnreadNotificationCounts
{
    [JsonPropertyName("highlight_count")]
    public int? HighlightCount { get; set; }

    [JsonPropertyName("notification_count")]
    public int? NotificationCount { get; set; }
}

public class InvitedRoom
{
    [JsonPropertyName("invite_state")]
    public InviteState? InviteState { get; set; }
}

public class JoinedRoom
{
    [JsonPropertyName("account_data")]
    public AccountData? AccountData { get; set; }

    [JsonPropertyName("ephemeral")]
    public Ephemeral? Ephemeral { get; set; }

    [JsonPropertyName("state")]
    public State? State { get; set; }

    [JsonPropertyName("summary")]
    public RoomSummary? Summary { get; set; }

    [JsonPropertyName("timeline")]
    public Timeline? Timeline { get; set; }

    [JsonPropertyName("unread_notifications")]
    public UnreadNotificationCounts? UnreadNotificationCounts { get; set; }
}

public class KnockState
{
    [JsonPropertyName("events")]
    public StrippedStateEvent[]? Events { get; set; }
}

public class KnockedRoom
{
    [JsonPropertyName("knock_state")]
    public KnockState? KnockState { get; set; }
}

public class LeftRoom
{
    [JsonPropertyName("account_data")]
    public AccountData? AccountData { get; set; }

    [JsonPropertyName("state")]
    public State? State { get; set; }

    [JsonPropertyName("timeline")]
    public Timeline? Timeline { get; set; }
}

public class Rooms
{
    [JsonPropertyName("invite")]
    public Dictionary<string, InvitedRoom>? Invite { get; set; }

    [JsonPropertyName("join")]
    public Dictionary<string, JoinedRoom>? Join { get; set; }

    [JsonPropertyName("knock")]
    public Dictionary<string, KnockedRoom>? Knock { get; set; }

    [JsonPropertyName("leave")]
    public Dictionary<string, LeftRoom>? Leave { get; set; }
}

public class DeviceLists
{
    [JsonPropertyName("changed")]
    public string[]? Changed { get; set; }

    [JsonPropertyName("left")]
    public string[]? Left { get; set; }
}

public class Presence
{
    [JsonPropertyName("events")]
    public Event[]? Events { get; set; }
}

public class ToDevice
{
    [JsonPropertyName("events")]
    public Event[]? Events { get; set; }
}

public class SyncResponse
{
    [JsonPropertyName("account_data")]
    public AccountData? AccountData { get; set; }

    [JsonPropertyName("device_lists")]
    public DeviceLists? DeviceLists { get; set; }

    [JsonPropertyName("device_one_time_keys_count")]
    public Dictionary<string, int>? OneTimeKeysCount { get; set; }

    [JsonPropertyName("device_unused_fallback_key_types")]
    public string[]? DeviceUnusedFallbackKeyAlgorithms { get; set; }

    [Required]
    [JsonPropertyName("next_batch")]
    public string NextBatch { get; set; } = default!;

    [JsonPropertyName("presence")]
    public Presence? Presence { get; set; }

    [JsonPropertyName("rooms")]
    public Rooms? Rooms { get; set; }

    [JsonPropertyName("to_device")]
    public ToDevice? ToDevice { get; set; }
}
