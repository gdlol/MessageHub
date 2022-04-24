using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.ClientServer.Protocol.Events.Room;

namespace MessageHub.HomeServer.RoomVersions.V9;

public class RoomStateResolver
{
    private readonly IRoomEventStore roomEventStore;

    public RoomStateResolver(IRoomEventStore roomEventStore)
    {
        ArgumentNullException.ThrowIfNull(roomEventStore);

        this.roomEventStore = roomEventStore;
    }

    private static bool IsPowerEvent(PersistentDataUnit pdu)
    {
        if (pdu.EventType == EventTypes.PowerLevels || pdu.EventType == EventTypes.JoinRules)
        {
            return true;
        }
        if (pdu.EventType == EventTypes.Member)
        {
            string? membership = pdu.Content.GetProperty(nameof(membership)).GetString();
            if (membership is null)
            {
                throw new InvalidOperationException();
            }
            if ((membership == MembershipStates.Leave || membership == MembershipStates.Ban)
                && (pdu.Sender != pdu.StateKey))
            {
                return true;
            }
        }
        return false;
    }

    private async ValueTask<int> GetSenderPowerLevelAsync(string eventId)
    {
        var pdu = await roomEventStore.LoadEventAsync(eventId);
        foreach (string authEventId in pdu.AuthorizationEvents)
        {
            var authPdu = await roomEventStore.LoadEventAsync(authEventId);
            if (authPdu.EventType == EventTypes.PowerLevels)
            {
                var powerLevelsEvent = JsonSerializer.Deserialize<PowerLevelsEvent>(authPdu.Content)!;
                if (powerLevelsEvent.Users?.TryGetValue(pdu.Sender, out int powerLevel) == true)
                {
                    return powerLevel;
                }
                return powerLevelsEvent.UsersDefault ?? 0;
            }
        }
        return pdu.Sender == roomEventStore.GetCreateEvent().Creator ? 100 : 0;
    }

    private async ValueTask<HashSet<string>> LoadAuthChainAsync(string eventId)
    {
        var states = await roomEventStore.LoadStatesAsync(eventId);
        var authEventIds = new HashSet<string>();
        foreach (string stateEventId in states.Values)
        {
            var pdu = await roomEventStore.LoadEventAsync(eventId);
            foreach (string authEventId in pdu.AuthorizationEvents)
            {
                authEventIds.Add(authEventId);
            }
        }
        var authChain = new HashSet<string>(authEventIds);
        while (authEventIds.Count > 0)
        {
            var newAuthEventIds = new HashSet<string>();
            foreach (string authEventId in authEventIds)
            {
                var pdu = await roomEventStore.LoadEventAsync(authEventId);
                foreach (string newAuthEventId in pdu.AuthorizationEvents)
                {
                    if (authChain.Add(newAuthEventId))
                    {
                        newAuthEventIds.Add(newAuthEventId);
                    }
                }
            }
            authEventIds = newAuthEventIds;
        }
        return authChain;
    }

    private async ValueTask<Dictionary<string, int>> GetMainlineOrdersAsync(string powerLevelsEventId)
    {
        var mainLine = new List<string> { powerLevelsEventId };
        PersistentDataUnit? parent = await roomEventStore.LoadEventAsync(powerLevelsEventId);
        while (parent is not null)
        {
            var authEventIds = parent.AuthorizationEvents;
            parent = null;
            foreach (string eventId in authEventIds)
            {
                var pdu = await roomEventStore.LoadEventAsync(eventId);
                if (pdu.EventType == EventTypes.PowerLevels)
                {
                    mainLine.Add(eventId);
                    parent = pdu;
                    break;
                }
            }
        }
        var orders = new Dictionary<string, int>();
        for (int i = 0; i < mainLine.Count; i++)
        {
            orders[mainLine[i]] = mainLine.Count - i;
        }
        orders[string.Empty] = 0;
        return orders;
    }

    private async ValueTask<string> GetClosestMainlineIdAsync(PersistentDataUnit pdu, Func<string, bool> isInMainline)
    {
        PersistentDataUnit? parent = pdu;
        while (parent is not null)
        {
            var authEventIds = parent.AuthorizationEvents;
            parent = null;
            foreach (string eventId in authEventIds)
            {
                var authPdu = await roomEventStore.LoadEventAsync(eventId);
                if (authPdu.EventType == EventTypes.PowerLevels)
                {
                    if (isInMainline(eventId))
                    {
                        return eventId;
                    }
                    parent = authPdu;
                    break;
                }
            }
        }
        return string.Empty;
    }

    private static string[] ReverseTopologicalPowerSort(
        Dictionary<string, HashSet<string>> controlEventChildren,
        Dictionary<string, int> powerLevels,
        Dictionary<string, PersistentDataUnit> controlEvents)
    {
        var eventOrder = controlEvents.Keys
            .OrderBy(x => x, Comparer<string>.Create((x, y) =>
            {
                int powerDifference = powerLevels[y].CompareTo(powerLevels[x]);
                if (powerDifference != 0)
                {
                    return powerDifference;
                }
                int timestampDifference = controlEvents[x].OriginServerTimestamp.CompareTo(
                    controlEvents[y].OriginServerTimestamp);
                if (timestampDifference != 0)
                {
                    return timestampDifference;
                }
                return x.CompareTo(y);
            }))
            .Select((key, index) => (key, index))
            .ToDictionary(x => x.key, x => x.index);

        var inDegrees = controlEventChildren.ToDictionary(x => x.Key, _ => 0);
        foreach (var children in controlEventChildren.Values)
        {
            foreach (string child in children)
            {
                inDegrees[child] += 1;
            }
        }
        var earliestNodes = new SortedList<string, int>(Comparer<string>.Create((x, y) =>
        {
            return eventOrder[x].CompareTo(eventOrder[y]);
        }));
        foreach (var (eventId, inDegree) in inDegrees)
        {
            if (inDegree == 0)
            {
                earliestNodes.Add(eventId, 0);
            }
        }
        var result = new List<string>();
        while (earliestNodes.Count > 0)
        {
            string eventId = earliestNodes.Keys[0];
            earliestNodes.RemoveAt(0);
            foreach (var child in controlEventChildren[eventId])
            {
                inDegrees[child] -= 1;
                if (inDegrees[child] == 0)
                {
                    earliestNodes.Add(child, 0);
                }
            }
            result.Add(eventId);
        }
        return result.ToArray();
    }

    public async ValueTask<ImmutableDictionary<RoomStateKey, string>> ResolveStateAsync(
        IEnumerable<string> previousEventIds)
    {
        ArgumentNullException.ThrowIfNull(previousEventIds);
        if (roomEventStore.IsEmpty)
        {
            throw new InvalidOperationException();
        }

        var previousEventIdsList = previousEventIds.ToList();
        if (previousEventIdsList.Count == 0)
        {
            throw new InvalidOperationException();
        }
        if (previousEventIdsList.Count == 1)
        {
            return await roomEventStore.LoadStatesAsync(previousEventIdsList[0]);
        }
        var branchStates = new List<ImmutableDictionary<RoomStateKey, string>>();
        foreach (var eventId in previousEventIdsList)
        {
            var states = await roomEventStore.LoadStatesAsync(eventId);
            branchStates.Add(states);
        }

        var unconflictedStates = new Dictionary<RoomStateKey, string>();
        var conflictedStates = new Dictionary<RoomStateKey, HashSet<string>>();
        var roomStateKeys = branchStates.SelectMany(x => x.Keys).ToHashSet();
        foreach (var stateKey in roomStateKeys)
        {
            var eventIds = new HashSet<string>();
            bool hasNull = false;
            foreach (var states in branchStates)
            {
                if (states.TryGetValue(stateKey, out string? eventId))
                {
                    eventIds.Add(eventId);
                }
                else
                {
                    hasNull = true;
                }
            }
            if (eventIds.Count == 1 && !hasNull)
            {
                unconflictedStates.Add(stateKey, eventIds.Single());
            }
            else
            {
                conflictedStates.Add(stateKey, eventIds);
            }
        }
        if (conflictedStates.Count == 0)
        {
            return unconflictedStates.ToImmutableDictionary();
        }

        var authChains = new List<HashSet<string>>();
        foreach (string eventId in previousEventIdsList)
        {
            var authChain = await LoadAuthChainAsync(eventId);
            authChains.Add(authChain);
        }
        var authIntersect = authChains.Aggregate(new HashSet<string>(authChains[0]), (x, y) =>
        {
            x.IntersectWith(y);
            return x;
        });
        var authDifference = authChains.Aggregate(new HashSet<string>(), (x, y) =>
        {
            x.UnionWith(y);
            return x;
        });
        authDifference.ExceptWith(authIntersect);

        var fullConflictedSet = new HashSet<string>();
        foreach (var eventIds in conflictedStates.Values)
        {
            fullConflictedSet.UnionWith(eventIds);
        }
        fullConflictedSet.UnionWith(authDifference);

        var controlEventChildren = new Dictionary<string, HashSet<string>>();
        foreach (string eventId in fullConflictedSet)
        {
            var pdu = await roomEventStore.LoadEventAsync(eventId);
            if (IsPowerEvent(pdu) && !controlEventChildren.ContainsKey(eventId))
            {
                controlEventChildren[eventId] = new HashSet<string>();
                var eventIds = new List<string> { eventId };
                while (eventIds.Count > 0)
                {
                    var newEventIds = new List<string>();
                    foreach (string childEventId in eventIds)
                    {
                        pdu = await roomEventStore.LoadEventAsync(childEventId);
                        foreach (string authEventId in pdu.AuthorizationEvents)
                        {
                            if (!fullConflictedSet.Contains(authEventId))
                            {
                                continue;
                            }
                            if (!controlEventChildren.ContainsKey(authEventId))
                            {
                                controlEventChildren[authEventId] = new HashSet<string>();
                                newEventIds.Add(authEventId);
                            }
                            controlEventChildren[authEventId].Add(childEventId);
                        }
                    }
                    eventIds = newEventIds;
                }
            }
        }
        var powerLevels = new Dictionary<string, int>();
        var controlEvents = new Dictionary<string, PersistentDataUnit>();
        foreach (string eventId in controlEventChildren.Keys)
        {
            powerLevels[eventId] = await GetSenderPowerLevelAsync(eventId);
            controlEvents[eventId] = await roomEventStore.LoadEventAsync(eventId);
        }
        var sortedControlEvents = ReverseTopologicalPowerSort(controlEventChildren, powerLevels, controlEvents);

        var resultBuilder = ImmutableDictionary.CreateBuilder<RoomStateKey, string>();
        resultBuilder.AddRange(unconflictedStates);

        var stateContents = new Dictionary<RoomStateKey, object>();
        foreach (var (roomStateKey, eventId) in unconflictedStates)
        {
            var pdu = await roomEventStore.LoadEventAsync(eventId);
            stateContents[roomStateKey] = ControlEventContentSerializer.TryDeserialize(pdu.EventType, pdu.Content);
        }
        var eventAuthorizer = new EventAuthorizer(roomEventStore.GetRoomId(), stateContents);
        foreach (string eventId in sortedControlEvents)
        {
            var controlEvent = controlEvents[eventId];
            if (controlEvent.StateKey is null)
            {
                throw new InvalidOperationException();
            }
            var roomStateKey = new RoomStateKey(controlEvent.EventType, controlEvent.StateKey);
            (var isAllowed, eventAuthorizer) = eventAuthorizer.TryUpdateState(
                roomStateKey,
                UserIdentifier.Parse(controlEvent.Sender),
                ControlEventContentSerializer.TryDeserialize(controlEvent.EventType, controlEvent.Content));
            if (isAllowed)
            {
                resultBuilder[roomStateKey] = eventId;
            }
        }

        var remainingEventIds = new List<string>();
        var remainingEvents = new Dictionary<string, PersistentDataUnit>();
        foreach (string eventId in fullConflictedSet.Except(sortedControlEvents))
        {
            var pdu = await roomEventStore.LoadEventAsync(eventId);
            remainingEventIds.Add(eventId);
            remainingEvents[eventId] = pdu;
        }
        if (!resultBuilder.TryGetValue(
            new RoomStateKey(EventTypes.PowerLevels, string.Empty),
            out var powerLevelsEventId))
        {
            remainingEventIds.Sort((x, y) =>
            {
                int timestampDifference = remainingEvents[x].OriginServerTimestamp.CompareTo(
                    remainingEvents[y].OriginServerTimestamp);
                if (timestampDifference != 0)
                {
                    return timestampDifference;
                }
                return x.CompareTo(y);
            });
        }
        else
        {
            var mainlineOrders = await GetMainlineOrdersAsync(powerLevelsEventId);
            var closestMainlineIds = new Dictionary<string, string>();
            foreach (var (eventId, pdu) in remainingEvents)
            {
                string closestMainlineId = await GetClosestMainlineIdAsync(pdu, mainlineOrders.ContainsKey);
                closestMainlineIds[eventId] = closestMainlineId;
            }
            remainingEventIds.Sort((x, y) =>
            {
                int mainlineOrderDifference = mainlineOrders[closestMainlineIds[x]].CompareTo(
                    mainlineOrders[closestMainlineIds[y]]);
                if (mainlineOrderDifference != 0)
                {
                    return mainlineOrderDifference;
                }
                int timestampDifference = remainingEvents[x].OriginServerTimestamp.CompareTo(
                    remainingEvents[y].OriginServerTimestamp);
                if (timestampDifference != 0)
                {
                    return timestampDifference;
                }
                return x.CompareTo(y);
            });
        }
        foreach (string eventId in remainingEventIds)
        {
            var remainingEvent = remainingEvents[eventId];
            if (remainingEvent.StateKey is null)
            {
                throw new InvalidOperationException();
            }
            var roomStateKey = new RoomStateKey(remainingEvent.EventType, remainingEvent.StateKey);
            (var isAllowed, eventAuthorizer) = eventAuthorizer.TryUpdateState(
                roomStateKey,
                UserIdentifier.Parse(remainingEvent.Sender),
                ControlEventContentSerializer.TryDeserialize(remainingEvent.EventType, remainingEvent.Content));
            if (isAllowed)
            {
                resultBuilder[roomStateKey] = eventId;
            }
        }

        return resultBuilder.ToImmutable();
    }
}
