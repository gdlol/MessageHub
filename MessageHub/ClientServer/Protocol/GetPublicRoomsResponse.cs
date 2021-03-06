using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class PublicRoomsChunk
{
    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("canonical_alias")]
    public string? CanonicalAlias { get; set; }

    [JsonPropertyName("guest_can_join")]
    public bool GuestCanJoin { get; set; }

    [JsonPropertyName("join_rule")]
    public string? JoinRule { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("num_joined_members")]
    public int JoinedMembersCount { get; set; }

    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = default!;

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("world_readable")]
    public bool WorldReadable { get; set; }
}

public class GetPublicRoomsResponse
{
    [JsonPropertyName("chunk")]
    public PublicRoomsChunk[] Chunk { get; set; } = default!;

    [JsonPropertyName("next_batch")]
    public string? NextBatch { get; set; }

    [JsonPropertyName("prev_batch")]
    public string? PreviousBatch { get; set; }

    [JsonPropertyName("total_room_count_estimate")]
    public int? TotalRoomCountEstimate { get; set; }
}
