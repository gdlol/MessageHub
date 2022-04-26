using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.ClientServer.Protocol.Events.Room;

namespace MessageHub.HomeServer;

public class EventCreator
{
    private readonly ImmutableDictionary<string, IRoom> rooms;
    private readonly IPeerIdentity identity;

    public EventCreator(ImmutableDictionary<string, IRoom> rooms, IPeerIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(identity);

        this.rooms = rooms;
        this.identity = identity;
    }

    public static string[] GetAuthorizationEventIds(
        ImmutableDictionary<RoomStateKey, string> states,
        string eventType,
        string? stateKey,
        string sender,
        JsonElement content)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        if (eventType == EventTypes.Create)
        {
            return Array.Empty<string>();
        }
        var result = new List<string>();
        string? eventId = states[new RoomStateKey(EventTypes.Create, string.Empty)];
        result.Add(eventId);
        if (states.TryGetValue(new RoomStateKey(EventTypes.PowerLevels, string.Empty), out eventId))
        {
            result.Add(eventId);
        }
        if (states.TryGetValue(new RoomStateKey(EventTypes.Member, sender), out eventId))
        {
            result.Add(eventId);
        }
        if (eventType == EventTypes.Member)
        {
            if (stateKey is null)
            {
                throw new InvalidOperationException();
            }
            if (sender != stateKey
                && states.TryGetValue(new RoomStateKey(EventTypes.Member, stateKey), out eventId))
            {
                result.Add(eventId);
            }
            var memberEvent = JsonSerializer.Deserialize<MemberEvent>(content);
            if (memberEvent is null)
            {
                throw new InvalidOperationException();
            }
            if ((memberEvent.MemberShip == MembershipStates.Join)
                || (memberEvent.MemberShip == MembershipStates.Invite))
            {
                if (states.TryGetValue(new RoomStateKey(EventTypes.JoinRules, string.Empty), out eventId))
                {
                    result.Add(eventId);
                }
            }
        }
        return result.ToArray();
    }

    public async ValueTask<JsonElement> CreateEventJsonAsync(
        string roomId,
        string eventType,
        string? stateKey,
        JsonElement content,
        long timestamp,
        JsonElement? unsigned = null)
    {
        var latestEventIds = Array.Empty<string>();
        var latestEvents = Array.Empty<PersistentDataUnit>();
        ImmutableDictionary<RoomStateKey, string> states;
        if (eventType == EventTypes.Create)
        {
            if (stateKey != string.Empty || rooms.ContainsKey(roomId))
            {
                throw new InvalidOperationException();
            }
            states = ImmutableDictionary<RoomStateKey, string>.Empty;
        }
        else
        {
            var room = rooms[roomId];
            latestEventIds = room.LatestEventIds.ToArray();
            var latestEventsList = new List<PersistentDataUnit>();
            foreach (string eventId in room.LatestEventIds)
            {
                var pdu = await room.EventStore.LoadEventAsync(eventId);
                latestEventsList.Add(pdu);
            }
            latestEvents = latestEventsList.ToArray();
            states = room.States;
        }

        var sender = UserIdentifier.FromId(identity.Id).ToString();
        var authorizationEvents = GetAuthorizationEventIds(states, eventType, stateKey, sender, content);
        long depth = latestEvents.Select(x => x.Depth).DefaultIfEmpty(0).Max() + 1;

        var result = new PersistentDataUnit
        {
            AuthorizationEvents = authorizationEvents,
            Content = content,
            Depth = depth,
            Origin = identity.Id,
            OriginServerTimestamp = timestamp,
            PreviousEvents = latestEventIds,
            Redacts = null,
            RoomId = roomId,
            Sender = sender,
            StateKey = stateKey,
            EventType = eventType,
            Unsigned = unsigned
        };
        EventHash.UpdateHash(result);
        var element = result.ToJsonElement();
        element = identity.SignJson(element);
        return element;
    }

    public async ValueTask<PersistentDataUnit> CreateEventAsync(
        string roomId,
        string eventType,
        string? stateKey,
        JsonElement content,
        long timestamp,
        JsonElement? unsigned = null)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        var element = await CreateEventJsonAsync(roomId, eventType, stateKey, content, timestamp, unsigned);
        return JsonSerializer.Deserialize<PersistentDataUnit>(element)!;
    }
}
