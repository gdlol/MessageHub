using System.Text.Json;

namespace MessageHub.HomeServer;

public interface IPersistenceService
{
    Task SaveAccountDataAsync(string? roomId, string eventType, JsonElement? value);
    Task<JsonElement?> LoadAccountDataAsync(string? roomId, string eventType);
    Task<(string eventType, JsonElement content)[]> LoadAccountDataAsync(
        string? roomId,
        Func<string, JsonElement, bool>? filter,
        int? limit);
    Task<string> SaveFilterAsync(string filter);
    Task<string?> LoadFilterAsync(string filterId);
}
