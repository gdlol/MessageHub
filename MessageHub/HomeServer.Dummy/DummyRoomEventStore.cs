using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.ClientServer.Protocol.Events.Room;

namespace MessageHub.HomeServer.Dummy;

public class DummyRoomEventStore : IRoomEventStore
{
    private readonly ImmutableDictionary<string, JsonElement> roomEvents;
    private readonly ImmutableDictionary<string, ImmutableDictionary<RoomStateKey, string>> statesHistory;
    private readonly RoomIdentifier roomId;
    private readonly CreateEvent createEvent;

    public bool IsEmpty => roomEvents.IsEmpty;

    private DummyRoomEventStore(
        ImmutableDictionary<string, JsonElement> roomEvents,
        ImmutableDictionary<string, ImmutableDictionary<RoomStateKey, string>> statesHistory,
        RoomIdentifier roomId,
        CreateEvent createEvent)
    {
        this.roomEvents = roomEvents;
        this.statesHistory = statesHistory;
        this.roomId = roomId;
        this.createEvent = createEvent;
    }

    public DummyRoomEventStore()
    {
        roomEvents = ImmutableDictionary<string, JsonElement>.Empty;
        statesHistory = ImmutableDictionary<string, ImmutableDictionary<RoomStateKey, string>>.Empty;
        roomId = new RoomIdentifier(string.Empty, string.Empty);
        createEvent = new CreateEvent { Creator = string.Empty };
    }

    public RoomIdentifier GetRoomId()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException();
        }
        return roomId;
    }

    public DummyRoomEventStore AddEvent(string eventId, PersistentDataUnit pdu, ImmutableDictionary<RoomStateKey, string> states)
    {
        ArgumentNullException.ThrowIfNull(pdu);
        ArgumentNullException.ThrowIfNull(states);

        var newRoomId = roomId;
        var newCreateEvent = createEvent;
        if (pdu.EventType == EventTypes.Create)
        {
            if (!IsEmpty)
            {
                throw new InvalidOperationException();
            }
            newRoomId = RoomIdentifier.Parse(pdu.RoomId);
            newCreateEvent = JsonSerializer.Deserialize<CreateEvent>(pdu.Content);
            if (newCreateEvent is null)
            {
                throw new InvalidOperationException();
            }
        }
        else
        {
            if (IsEmpty)
            {
                throw new InvalidOperationException();
            }
        }
        var newEvents = roomEvents.Add(eventId, pdu.ToJsonElement());
        var newStateshistory = statesHistory.Add(eventId, states);
        return new DummyRoomEventStore(newEvents, newStateshistory, newRoomId, newCreateEvent);
    }

    public CreateEvent GetCreateEvent()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException();
        }
        return createEvent;
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
