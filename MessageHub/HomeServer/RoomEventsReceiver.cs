using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer;

public class RoomEventsReceiver
{
    private readonly string roomId;
    private readonly IIdentityService identityService;
    private readonly IRoomEventStore roomEventStore;
    private readonly IEventSaver eventSaver;
    private readonly UnresolvedEventNotifier unresolvedEventNotifier;

    public RoomEventsReceiver(
        string roomId,
        IIdentityService identityService,
        IRoomEventStore roomEventStore,
        IEventSaver eventSaver,
        UnresolvedEventNotifier unresolvedEventNotifier)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(roomEventStore);
        ArgumentNullException.ThrowIfNull(eventSaver);
        ArgumentNullException.ThrowIfNull(unresolvedEventNotifier);

        this.roomId = roomId;
        this.identityService = identityService;
        this.roomEventStore = roomEventStore;
        this.eventSaver = eventSaver;
        this.unresolvedEventNotifier = unresolvedEventNotifier;
    }

    public static bool ValidateSender(PersistentDataUnit pdu)
    {
        ArgumentNullException.ThrowIfNull(pdu);

        if (!UserIdentifier.TryParse(pdu.Sender, out var senderIdentifier))
        {
            return false;
        }
        if (senderIdentifier.Id != pdu.Origin)
        {
            return false;
        }
        return true;
    }

    private bool VerifySignature(PersistentDataUnit pdu)
    {
        if (pdu.ServerKeys.ValidUntilTimestamp < pdu.OriginServerTimestamp)
        {
            return false;
        }
        return identityService.VerifyJson(pdu.Origin, pdu.ToJsonElement());
    }

    public (bool, string) ValidateEvent(PersistentDataUnit pdu)
    {
        if (pdu.RoomId != roomId)
        {
            return (false, $"{nameof(roomId)}: {pdu.RoomId}");
        }
        string? eventId = EventHash.TryGetEventId(pdu);
        if (eventId is null)
        {
            return (false, EventReceiveErrors.InvalidEventId);
        }
        bool isValidEvent = ValidateSender(pdu);
        if (!isValidEvent)
        {
            return (false, $"{nameof(isValidEvent)}: {isValidEvent}");
        }
        bool isSignatureValid = VerifySignature(pdu);
        if (!isSignatureValid)
        {
            return (false, $"{nameof(isSignatureValid)}: {isSignatureValid}");
        }
        bool isHashValid = EventHash.VerifyHash(pdu);
        if (!isHashValid)
        {
            return (false, $"{nameof(isHashValid)}: {isHashValid}");
        }
        return (true, string.Empty);
    }

    public async Task<Dictionary<string, string?>> ReceiveEventsAsync(IEnumerable<PersistentDataUnit> pdus)
    {
        var errors = new Dictionary<string, string?>();
        var events = new Dictionary<string, PersistentDataUnit>();

        // Validate events.
        foreach (var pdu in pdus)
        {
            var (isValid, error) = ValidateEvent(pdu);
            if (isValid)
            {
                string eventId = EventHash.GetEventId(pdu);
                errors[eventId] = null;
                events[eventId] = pdu;
            }
            else
            {
                if (EventHash.TryGetEventId(pdu) is string eventId)
                {
                    errors[eventId] = error;
                }
            }
        }
        var newEventIds = await roomEventStore.GetMissingEventIdsAsync(events.Keys);
        foreach (string existingEventId in events.Keys.Except(newEventIds).ToArray())
        {
            events.Remove(existingEventId);
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
                    if (children.Add(eventId))
                    {
                        outDegrees[eventId] += 1;
                    }
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
        var unresolvedEvents = new List<PersistentDataUnit>();
        foreach (string eventId in events.Keys.Except(sortedResolvedEvents))
        {
            errors[eventId] = EventReceiveErrors.NotResolved;
            unresolvedEvents.Add(events[eventId]);
        }
        if (unresolvedEvents.Count > 0)
        {
            unresolvedEventNotifier.Notify(unresolvedEvents.ToArray());
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
            if (isAuthorized)
            {
                newStates = pdu.StateKey is null
                    ? statesBefore
                    : statesBefore.SetItem(new RoomStateKey(pdu.EventType, pdu.StateKey), eventId);
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
                errors[eventId] = EventReceiveErrors.Rejected;
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
