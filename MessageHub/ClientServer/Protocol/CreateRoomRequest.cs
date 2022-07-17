using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class Invite3pid
{
    [Required]
    [JsonPropertyName("address")]
    public string Address { get; set; } = default!;

    [Required]
    [JsonPropertyName("id_access_token")]
    public string IdAccessToken { get; set; } = default!;

    [Required]
    [JsonPropertyName("id_server")]
    public string IdServer { get; set; } = default!;

    [Required]
    [JsonPropertyName("medium")]
    public string Medium { get; set; } = default!;
}

public class CreateRoomRequest
{
    [JsonPropertyName("creation_content")]
    public JsonElement? CreationContent { get; set; }

    [JsonPropertyName("initial_state")]
    public StateEvent[]? InitialState { get; set; }

    [JsonPropertyName("invite")]
    public string[]? Invite { get; set; }

    [JsonPropertyName("invite_3pid")]
    public Invite3pid[]? Invite3pid { get; set; }

    [JsonPropertyName("is_direct")]
    public bool? IsDirect { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("power_level_content_override")]
    public JsonElement? PowerLevelContentOverride { get; set; }

    [JsonPropertyName("preset")]
    public string? Preset { get; set; }

    [JsonPropertyName("room_alias_name")]
    public string? RoomAliasName { get; set; }

    [JsonPropertyName("room_version")]
    public string? RoomVersion { get; set; }

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("visibility")]
    public string? Visibility { get; set; }
}
