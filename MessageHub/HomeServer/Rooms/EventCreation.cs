using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;

namespace MessageHub.HomeServer.Rooms;

public static class EventCreation
{
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
            if (states.Count > 0)
            {
                throw new InvalidOperationException();
            }
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

    public static (RoomSnapshot, PersistentDataUnit) CreateEvent(
        string roomId,
        RoomSnapshot snapshot,
        string eventType,
        string? stateKey,
        ServerKeys serverKeys,
        UserIdentifier sender,
        JsonElement content,
        long timestamp,
        string? redacts = null,
        JsonElement? unsigned = null)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        var authorizationEvents = GetAuthorizationEventIds(
            snapshot.States,
            eventType,
            stateKey,
            sender.ToString(),
            content);
        var pdu = new PersistentDataUnit
        {
            AuthorizationEvents = authorizationEvents,
            Content = content,
            Depth = snapshot.GraphDepth + 1,
            Origin = sender.PeerId,
            OriginServerTimestamp = timestamp,
            PreviousEvents = snapshot.LatestEventIds.ToArray(),
            Redacts = redacts,
            RoomId = roomId,
            ServerKeys = serverKeys,
            Sender = sender.ToString(),
            StateKey = stateKey,
            EventType = eventType,
            Unsigned = unsigned
        };
        EventHash.UpdateHash(pdu);
        string eventId = EventHash.GetEventId(pdu);
        var newStates = snapshot.States;
        var newStateContents = snapshot.StateContents;
        if (stateKey is not null)
        {
            newStates = newStates.SetItem(new RoomStateKey(eventType, stateKey), eventId);
        }
        var newSnapshot = new RoomSnapshot
        {
            LatestEventIds = new[] { eventId }.ToImmutableList(),
            GraphDepth = pdu.Depth,
            States = newStates,
            StateContents = newStateContents
        };
        return (newSnapshot, pdu);
    }
}
