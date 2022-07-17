using System.Text.Json.Serialization;

namespace MessageHub.ClientServer.Protocol;

public class SearchUserRequest
{
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("search_term")]
    public string SearchTerm { get; set; } = default!;
}
