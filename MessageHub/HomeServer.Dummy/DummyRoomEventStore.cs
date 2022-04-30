using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Rooms;

namespace MessageHub.HomeServer.Dummy;

public class DummyRoomEventStore : IRoomEventStore
{
    public string Creator { get; }

    private readonly ImmutableDictionary<string, JsonElement> roomEvents;
    private readonly ImmutableDictionary<string, ImmutableDictionary<RoomStateKey, string>> statesHistory;

    private DummyRoomEventStore(
        string creator,
        ImmutableDictionary<string, JsonElement> roomEvents,
        ImmutableDictionary<string, ImmutableDictionary<RoomStateKey, string>> statesHistory)
    {
        Creator = creator;
        this.roomEvents = roomEvents;
        this.statesHistory = statesHistory;
    }

    public DummyRoomEventStore(string creator)
    {
        ArgumentNullException.ThrowIfNull(creator);

        Creator = creator;
        roomEvents = ImmutableDictionary<string, JsonElement>.Empty;
        statesHistory = ImmutableDictionary<string, ImmutableDictionary<RoomStateKey, string>>.Empty;
    }

    public DummyRoomEventStore AddEvent(string eventId, PersistentDataUnit pdu, ImmutableDictionary<RoomStateKey, string> states)
    {
        ArgumentNullException.ThrowIfNull(pdu);
        ArgumentNullException.ThrowIfNull(states);

        var newEvents = roomEvents.Add(eventId, pdu.ToJsonElement());
        var newStateshistory = statesHistory.Add(eventId, states);
        return new DummyRoomEventStore(Creator, newEvents, newStateshistory);
    }

    public Task<string[]> GetMissingEventIdsAsync(IEnumerable<string> eventIds)
    {
        ArgumentNullException.ThrowIfNull(eventIds);

        var result = eventIds.Except(roomEvents.Keys).ToArray();
        return Task.FromResult(result);
    }

    public ValueTask<PersistentDataUnit> LoadEventAsync(string eventId)
    {
        ArgumentNullException.ThrowIfNull(eventId);

        var element = roomEvents[eventId];
        var result = JsonSerializer.Deserialize<PersistentDataUnit>(element)!;
        return ValueTask.FromResult(result);
    }

    public ValueTask<ImmutableDictionary<RoomStateKey, string>> LoadStatesAsync(string eventId)
    {
        ArgumentNullException.ThrowIfNull(eventId);

        var result = statesHistory[eventId];
        return ValueTask.FromResult(result);
    }
}
