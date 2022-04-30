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
            GraphDepth = pdu.Depth + 1,
            States = states,
            StateContents = stateContentsBuilder.ToImmutable()
        };
    }
}
