using System.Collections.Immutable;
using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.Rooms;

public class RoomEventStoreBuilder : IRoomEventStore
{
    public IRoomEventStore BaseStore { get; }

    private readonly Dictionary<string, PersistentDataUnit> newEvents = new();
    private readonly Dictionary<string, ImmutableDictionary<RoomStateKey, string>> newStates = new();

    public IReadOnlyDictionary<string, PersistentDataUnit> NewEvents => newEvents;
    public IReadOnlyDictionary<string, ImmutableDictionary<RoomStateKey, string>> NewStates => newStates;

    public string Creator => BaseStore.Creator;

    public RoomEventStoreBuilder(IRoomEventStore baseStore)
    {
        ArgumentNullException.ThrowIfNull(baseStore);

        BaseStore = baseStore;
    }

    public Task<string[]> GetMissingEventIdsAsync(IEnumerable<string> eventIds)
    {
        return BaseStore.GetMissingEventIdsAsync(eventIds.Except(newEvents.Keys));
    }

    public async ValueTask<PersistentDataUnit> LoadEventAsync(string eventId)
    {
        if (newEvents.TryGetValue(eventId, out var pdu))
        {
            return pdu!;
        }
        pdu = await BaseStore.LoadEventAsync(eventId);
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
        newEvents.Add(eventId, pdu);
        newStates.Add(eventId, states);
    }
}
