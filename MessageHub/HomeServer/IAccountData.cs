using System.Text.Json;

namespace MessageHub.HomeServer;

public interface IAccountData
{
    Task SaveAccountDataAsync(string? roomId, string eventType, JsonElement? value);
    Task<JsonElement?> LoadAccountDataAsync(string? roomId, string eventType);
    Task<(string eventType, JsonElement content)[]> LoadAccountDataAsync(
        string? roomId,
        Func<string, JsonElement, bool>? filter,
        int? limit);
    Task<string> SaveFilterAsync(string filter);
    Task<string?> LoadFilterAsync(string filterId);
    Task<string?> GetRoomVisibilityAsync(string roomId);
    Task<bool> SetRoomVisibilityAsync(string roomId, string visibility);
    Task<string[]> GetPublicRoomListAsync();
}
