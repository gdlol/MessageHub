using System.Text.Json;

namespace MessageHub.HomeServer;

public interface IPersistenceService
{
    Task SaveAccountDataAsync(string userId, string? roomId, string eventType, JsonElement? value);
    Task<JsonElement?> LoadAccountDataAsync(string userId, string? roomId, string eventType);
    Task<string> SaveFilterAsync(string userId, string filter);
    Task<string?> LoadFilterAsync(string userId, string filterId);
}
