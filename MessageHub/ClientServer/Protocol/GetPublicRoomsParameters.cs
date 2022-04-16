using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class GetPublicRoomsParameters
{
    [JsonPropertyName("filter")]
    public JsonElement? Filter { get; set; }

    [JsonPropertyName("include_all_networks")]
    public bool IncludeAllNetworks { get; set; } = false;

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("since")]
    public string? Since { get; set; }

    [JsonPropertyName("third_party_instance_id")]
    public string? ThirdPartyInstanceId { get; set; }
}
