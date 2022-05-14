using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Formatting;
using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Rooms;

namespace MessageHub.HomeServer.P2p.Rooms;

public class RoomEventStore : IRoomEventStore
{
    private readonly IKeyValueStore store;

    public string Creator { get; }

    public RoomEventStore(string creator, IKeyValueStore store)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(store);

        Creator = creator;
        this.store = store;
    }

    private static string GetEventKey(string eventId) => $"Event-{eventId}";

    private static string GetStateKey(string eventId) => $"State-{eventId}";

    public async Task<string[]> GetMissingEventIdsAsync(IEnumerable<string> eventIds)
    {
        var result = new List<string>();
        foreach (string eventId in eventIds)
        {
            string eventKey = GetEventKey(eventId);
            var value = await store.GetAsync(eventKey);
            if (value is null)
            {
                result.Add(eventId);
            }
        }
        return result.ToArray();
    }

    public async ValueTask<PersistentDataUnit> LoadEventAsync(string eventId)
    {
        string eventKey = GetEventKey(eventId);
        var value = await store.GetAsync(eventKey);
        if (value is null)
        {
            throw new KeyNotFoundException(eventKey);
        }
        return JsonSerializer.Deserialize<PersistentDataUnit>(value)!;
    }

    public async ValueTask<ImmutableDictionary<RoomStateKey, string>> LoadStatesAsync(string eventId)
    {
        string stateKey = GetStateKey(eventId);
        var value = await store.GetAsync(stateKey);
        if (value is null)
        {
            throw new KeyNotFoundException(stateKey);
        }
        var states = JsonSerializer.Deserialize<Dictionary<string, string>>(value)!;
        return states.ToImmutableDictionary(
            x => JsonSerializer.Deserialize<RoomStateKey>(x.Key)!,
            x => x.Value);
    }

    public async ValueTask AddEvent(string eventId, PersistentDataUnit pdu, ImmutableDictionary<RoomStateKey, string> states)
    {
        ArgumentNullException.ThrowIfNull(pdu);
        ArgumentNullException.ThrowIfNull(states);

        string eventKey = GetEventKey(eventId);
        string stateKey = GetStateKey(eventId);
        await store.PutAsync(eventId, CanonicalJson.SerializeToBytes(pdu.ToJsonElement()));
        var statesValue = states.ToDictionary(x => JsonSerializer.Serialize(x.Key), x => x.Value);
        using var stateStream = new MemoryStream();
        JsonSerializer.Serialize(stateStream, statesValue);
        await store.PutAsync(stateKey, stateStream.ToArray());
    }
}
