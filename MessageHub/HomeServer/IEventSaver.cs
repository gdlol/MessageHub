using System.Collections.Immutable;
using System.Text.Json;

namespace MessageHub.HomeServer;

public interface IEventSaver
{
    Task SaveAsync(
        string roomId,
        string eventId,
        JsonElement element,
        IReadOnlyDictionary<RoomStateKey, string> states);
    Task SaveBatchAsync(
        string roomId,
        IReadOnlyList<string> eventIds,
        IReadOnlyDictionary<string, JsonElement> events,
        IReadOnlyDictionary<string, ImmutableDictionary<RoomStateKey, string>> states);
}
