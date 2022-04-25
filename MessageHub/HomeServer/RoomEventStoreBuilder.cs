using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.ClientServer.Protocol.Events.Room;

namespace MessageHub.HomeServer;

public class RoomEventStoreBuilder : IRoomEventStore
{
    public IRoomEventStore BaseStore { get; }

    private readonly Dictionary<string, JsonElement> newEvents = new();
    private readonly Dictionary<string, ImmutableDictionary<RoomStateKey, string>> newStates = new();
    private RoomIdentifier? roomId;
    private CreateEvent? createEvent;

    public IReadOnlyDictionary<string, JsonElement> NewEvents => newEvents;
    public IReadOnlyDictionary<string, ImmutableDictionary<RoomStateKey, string>> NewStates => newStates;

    public RoomEventStoreBuilder(IRoomEventStore baseStore)
    {
        ArgumentNullException.ThrowIfNull(baseStore);

        BaseStore = baseStore;
    }

    public bool IsEmpty => BaseStore.IsEmpty && newEvents.Count == 0;

    public CreateEvent GetCreateEvent()
    {
        if (createEvent is not null)
        {
            return createEvent;
        }
        if (!BaseStore.IsEmpty)
        {
            createEvent = BaseStore.GetCreateEvent();
            return createEvent;
        }
        throw new InvalidOperationException();
    }

    public Task<string[]> GetMissingEventIdsAsync(IEnumerable<string> eventIds)
    {
        return BaseStore.GetMissingEventIdsAsync(eventIds.Except(newEvents.Keys));
    }

    public RoomIdentifier GetRoomId()
    {
        if (roomId is not null)
        {
            return roomId;
        }
        if (!BaseStore.IsEmpty)
        {
            roomId = BaseStore.GetRoomId();
            return roomId;
        }
        throw new InvalidOperationException();
    }

    public async ValueTask<PersistentDataUnit> LoadEventAsync(string eventId)
    {
        if (newEvents.TryGetValue(eventId, out var element))
        {
            return JsonSerializer.Deserialize<PersistentDataUnit>(element)!;
        }
        var pdu = await BaseStore.LoadEventAsync(eventId);
        return pdu;
    }

    public async ValueTask<ImmutableDictionary<RoomStateKey, string>> LoadStatesAsync(string eventId)
    {
        if (newStates.TryGetValue(eventId, out var states))
        {
            return states;
        }
        states = await BaseStore.LoadStatesAsync(eventId);
        return states;
    }

    public void AddEvent(string eventId, PersistentDataUnit pdu, ImmutableDictionary<RoomStateKey, string> states)
    {
        newEvents.Add(eventId, pdu.ToJsonElement());
        newStates.Add(eventId, states);
    }
}
