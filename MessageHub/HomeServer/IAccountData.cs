using System.Text.Json;

namespace MessageHub.HomeServer;

public interface IAccountData
{
    Task SaveAccountDataAsync(string? roomId, string eventType, JsonElement? value);
    Task<JsonElement?> LoadAccountDataAsync(string? roomId, string eventType);
    IAsyncEnumerable<(string eventType, JsonElement content)> LoadAccountDataAsync(string? roomId);
    Task<string> SaveFilterAsync(string filter);
    Task<string?> LoadFilterAsync(string filterId);
    Task<string?> GetRoomVisibilityAsync(string roomId);
    Task<bool> SetRoomVisibilityAsync(string roomId, string visibility);
    Task<string[]> GetPublicRoomListAsync();
}
