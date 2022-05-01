using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.Rooms;

public interface IRoomEventStore
{
    string Creator { get; }
    Task<string[]> GetMissingEventIdsAsync(IEnumerable<string> eventIds);
    ValueTask<PersistentDataUnit> LoadEventAsync(string eventId);
    ValueTask<ImmutableDictionary<RoomStateKey, string>> LoadStatesAsync(string eventId);

    public async Task<PersistentDataUnit?> TryLoadEventAsync(string eventId)
    {
        var missingEventIds = await GetMissingEventIdsAsync(new[] { eventId });
        if (missingEventIds.Length > 0)
        {
            return null;
        }
        var pdu = await LoadEventAsync(eventId);
        return pdu;
    }

    public async ValueTask<RoomSnapshot> LoadSnapshotAsync(string eventId)
    {
        var pdu = await LoadEventAsync(eventId);
        var states = await LoadStatesAsync(eventId);
        var stateContentsBuilder = ImmutableDictionary.CreateBuilder<RoomStateKey, JsonElement>();
        foreach (var (roomStateKey, stateEventId) in states)
        {
            pdu = await LoadEventAsync(stateEventId);
            stateContentsBuilder[roomStateKey] = pdu.Content;
        }
        return new RoomSnapshot
        {
            LatestEventIds = ImmutableList.Create(eventId),
            GraphDepth = pdu.Depth,
            States = states,
            StateContents = stateContentsBuilder.ToImmutable()
        };
    }
}
