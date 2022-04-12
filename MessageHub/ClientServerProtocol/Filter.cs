using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MessageHub.ClientServerProtocol;

public class EventFilter
{
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("not_senders")]
    public string[]? NotSenders { get; set; }

    [JsonPropertyName("not_types")]
    public string[]? NotTypes { get; set; }

    [JsonPropertyName("senders")]
    public string[]? Senders { get; set; }

    [JsonPropertyName("types")]
    public string[]? Types { get; set; }
}

public class RoomEventFilter
{
    [JsonPropertyName("contains_url")]
    public bool? ContainsUrl { get; set; }

    [JsonPropertyName("include_redundant_members")]
    public bool? IncludeRedundantMembers { get; set; }

    [JsonPropertyName("lazy_load_members")]
    public bool? LazyLoadMembers { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("not_rooms")]
    public string[]? NotRooms { get; set; }

    [JsonPropertyName("not_senders")]
    public string[]? NotSenders { get; set; }

    [JsonPropertyName("not_types")]
    public string[]? NotTypes { get; set; }

    [JsonPropertyName("rooms")]
    public string[]? Rooms { get; set; }

    [JsonPropertyName("senders")]
    public string[]? Senders { get; set; }

    [JsonPropertyName("types")]
    public string[]? Types { get; set; }
}

public class StateFilter
{
    [JsonPropertyName("contains_url")]
    public bool? ContainsUrl { get; set; }

    [JsonPropertyName("include_redundant_members")]
    public bool? IncludeRedundantMembers { get; set; }

    [JsonPropertyName("lazy_load_members")]
    public bool? LazyLoadMembers { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("not_rooms")]
    public string[]? NotRooms { get; set; }

    [JsonPropertyName("not_senders")]
    public string[]? NotSenders { get; set; }

    [JsonPropertyName("not_types")]
    public string[]? NotTypes { get; set; }

    [JsonPropertyName("rooms")]
    public string[]? Rooms { get; set; }

    [JsonPropertyName("senders")]
    public string[]? Senders { get; set; }

    [JsonPropertyName("types")]
    public string[]? Types { get; set; }
}

public class RoomFilter
{
    [JsonPropertyName("account_data")]
    public RoomEventFilter? AccountData { get; set; }

    [JsonPropertyName("ephemeral")]
    public RoomEventFilter? Ephemeral { get; set; }

    [JsonPropertyName("include_leave")]
    public bool? IncludeLeave { get; set; }

    [JsonPropertyName("not_rooms")]
    public string[]? NotRooms { get; set; }

    [JsonPropertyName("rooms")]
    public string[]? Rooms { get; set; }

    [JsonPropertyName("state")]
    public StateFilter? State { get; set; }

    [JsonPropertyName("timeline")]
    public RoomEventFilter? Timeline { get; set; }
}

public class Filter
{
    [JsonPropertyName("account_data")]
    public EventFilter? AccountData { get; set; }

    [JsonPropertyName("event_fields")]
    public string[]? EventFields { get; set; }

    [JsonPropertyName("event_format")]
    public string? EventFormat { get; set; }

    [JsonPropertyName("presence")]
    public EventFilter? Presence { get; set; }

    [JsonPropertyName("room")]
    public RoomFilter? Room { get; set; }

    public static bool StringMatch(string input, string pattern)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(pattern);

        pattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*") + "$";
        return Regex.IsMatch(input, pattern);
    }
}
