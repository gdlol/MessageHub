using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Formatting;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer;

public class RoomEventsReceiver
{
    private readonly string roomId;
    private readonly IPeerIdentity identity;
    private readonly IPeerStore peerStore;
    private readonly IRoomEventStore roomEventStore;
    private readonly IEventSaver eventSaver;

    public RoomEventsReceiver(
        string roomId,
        IPeerIdentity identity,
        IPeerStore peerStore,
        IRoomEventStore roomEventStore,
        IEventSaver eventSaver)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(peerStore);
        ArgumentNullException.ThrowIfNull(roomEventStore);
        ArgumentNullException.ThrowIfNull(eventSaver);

        this.roomId = roomId;
        this.identity = identity;
        this.peerStore = peerStore;
        this.roomEventStore = roomEventStore;
        this.eventSaver = eventSaver;
    }

    public static bool ValidateSender(PersistentDataUnit pdu)
    {
        ArgumentNullException.ThrowIfNull(pdu);

        if (!UserIdentifier.TryParse(pdu.Sender, out var senderIdentifier))
        {
            return false;
        }
        if (senderIdentifier.PeerId != pdu.Origin)
        {
            return false;
        }
        return true;
    }

    private bool VerifySignature(PersistentDataUnit pdu)
    {
        if (!peerStore.TryGetPeer(pdu.Origin, out var peer))
        {
            return false;
        }
        return identity.VerifyJson(peer, pdu.ToJsonElement());
    }

    public static bool VerifyHash(PersistentDataUnit pdu)
    {
        ArgumentNullException.ThrowIfNull(pdu);

        if (pdu.Hashes.SingleOrDefault() is not (string algorithm, string hash)
            || algorithm != "sha256"
            || hash != UnpaddedBase64Encoder.Encode(EventHash.ComputeHash(pdu)))
        {
            return false;
        }
        return true;
    }

    public async Task<Dictionary<string, string?>> ReceiveEvents(IEnumerable<PersistentDataUnit> pdus)
    {
        var errors = new Dictionary<string, string?>();
        var events = new Dictionary<string, PersistentDataUnit>();

        // Validate events.
        foreach (var pdu in pdus)
        {
            if (pdu.RoomId != roomId)
            {
                continue;
            }
            string? eventId = EventHash.TryGetEventId(pdu);
            if (eventId is null)
            {
                continue;
            }
            bool isValidEvent = ValidateSender(pdu);
            if (!isValidEvent)
            {
                errors[eventId] = $"{nameof(isValidEvent)}: {isValidEvent}";
                continue;
            }
            bool isSignatureValid = VerifySignature(pdu);
            if (!isSignatureValid)
            {
                errors[eventId] = $"{nameof(isSignatureValid)}: {isSignatureValid}";
                continue;
            }
            bool isHashValid = VerifyHash(pdu);
            if (!isHashValid)
            {
                errors[eventId] = $"{nameof(isHashValid)}: {isHashValid}";
                continue;
            }
            errors[eventId] = null;
            events[eventId] = pdu;
        }

        // build Partial graph.
        var dependentEvents = events.Keys.ToDictionary(x => x, x => new HashSet<string>());
        var outDegrees = events.Keys.ToDictionary(x => x, _ => 0);
        foreach (var (eventId, pdu) in events)
        {
            foreach (string previousEventId in pdu.AuthorizationEvents.Union(pdu.PreviousEvents))
            {
                if (dependentEvents.TryGetValue(previousEventId, out var children))
                {
                    children.Add(eventId);
                    outDegrees[eventId] += 1;
                }
            }
        }

        // Sort resolved events.
        var dependingEventIds = events.Values
            .SelectMany(x => x.AuthorizationEvents.Concat(x.PreviousEvents))
            .Where(eventId => !events.ContainsKey(eventId))
            .Distinct();
        var missingEventIdsList = await roomEventStore.GetMissingEventIdsAsync(dependingEventIds);
        var missingEventIds = new HashSet<string>(missingEventIdsList);
        var sortedResolvedEvents = new List<string>();
        var earliestEvents = outDegrees.Where(x => x.Value == 0).Select(x => x.Key).ToList();
        while (earliestEvents.Count > 0)
        {
            var newEarliestEvents = new List<string>();
            foreach (var eventId in earliestEvents)
            {
                var pdu = events[eventId];
                if (pdu.AuthorizationEvents.Any(missingEventIds.Contains)
                    || pdu.PreviousEvents.Any(missingEventIds.Contains))
                {
                    continue;
                }
                foreach (var child in dependentEvents[eventId])
                {
                    outDegrees[child] -= 1;
                    if (outDegrees[child] == 0)
                    {
                        newEarliestEvents.Add(child);
                    }
                }
                sortedResolvedEvents.Add(eventId);
            }
            earliestEvents = newEarliestEvents;
        }
        foreach (string eventId in events.Keys.Except(sortedResolvedEvents))
        {
            errors[eventId] = "Not resolved.";
        }

        // Authorize events.
        RoomEventStoreBuilder builder = new(roomEventStore);
        var roomStateResolver = new RoomStateResolver(builder);
        var acceptedEvents = new List<string>();
        var rejectedEvents = new HashSet<string>();
        async ValueTask<bool> authorizeOnStatesAsync(
            ImmutableDictionary<RoomStateKey, string> states,
            PersistentDataUnit pdu)
        {
            var stateContentBuilder = ImmutableDictionary.CreateBuilder<RoomStateKey, JsonElement>();
            foreach (var (roomStateKey, stateEventId) in states)
            {
                var statePdu = await builder.LoadEventAsync(stateEventId);
                stateContentBuilder[roomStateKey] = statePdu.Content;
            }
            var authorizer = new EventAuthorizer(stateContentBuilder.ToImmutable());
            return authorizer.Authorize(pdu.EventType, pdu.StateKey, UserIdentifier.Parse(pdu.Sender), pdu.Content);
        }
        async ValueTask<(bool, ImmutableDictionary<RoomStateKey, string>)> authorizeAsync(
            string eventId,
            PersistentDataUnit pdu)
        {
            var newStates = ImmutableDictionary<RoomStateKey, string>.Empty;
            if (pdu.AuthorizationEvents.Any(rejectedEvents.Contains)
                || pdu.PreviousEvents.Any(rejectedEvents.Contains))
            {
                return (false, newStates);
            }
            if (pdu.AuthorizationEvents.Distinct().Count() != pdu.AuthorizationEvents.Length)
            {
                return (false, newStates);
            }
            if (pdu.PreviousEvents.Distinct().Count() != pdu.PreviousEvents.Length)
            {
                return (false, newStates);
            }
            var statesBefore = await roomStateResolver.ResolveStateAsync(pdu.AuthorizationEvents);
            var expectedAuthEventIds = EventCreation.GetAuthorizationEventIds(
                statesBefore,
                pdu.EventType,
                pdu.StateKey,
                pdu.Sender,
                pdu.Content);
            if (!expectedAuthEventIds.ToHashSet().SetEquals(pdu.AuthorizationEvents.ToHashSet()))
            {
                return (false, newStates);
            }
            bool isAuthorized = await authorizeOnStatesAsync(statesBefore, pdu);
            if (!isAuthorized)
            {
                return (false, newStates);
            }

            statesBefore = await roomStateResolver.ResolveStateAsync(pdu.PreviousEvents);
            expectedAuthEventIds = EventCreation.GetAuthorizationEventIds(
                statesBefore,
                pdu.EventType,
                pdu.StateKey,
                pdu.Sender,
                pdu.Content);
            if (!expectedAuthEventIds.ToHashSet().SetEquals(pdu.AuthorizationEvents.ToHashSet()))
            {
                return (false, newStates);
            }
            isAuthorized = await authorizeOnStatesAsync(statesBefore, pdu);
            if (isAuthorized && pdu.StateKey != null)
            {
                newStates = statesBefore.SetItem(new RoomStateKey(pdu.EventType, pdu.StateKey), eventId);
                return (true, newStates);
            }
            else
            {
                return (false, newStates);
            }
        }
        foreach (string eventId in sortedResolvedEvents)
        {
            var pdu = events[eventId];
            var (isAuthorized, newStates) = await authorizeAsync(eventId, pdu);
            if (isAuthorized)
            {
                builder.AddEvent(eventId, pdu, newStates);
                acceptedEvents.Add(eventId);
            }
            else
            {
                rejectedEvents.Add(eventId);
                errors[eventId] = "Rejected";
            }
        }

        {
            var newEvents = builder.NewEvents.ToImmutableDictionary();
            var newStates = builder.NewStates.ToImmutableDictionary();
            await eventSaver.SaveBatchAsync(roomId, acceptedEvents, newEvents, newStates);
        }

        return errors;
    }
}
