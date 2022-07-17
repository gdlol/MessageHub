using System.Collections.Concurrent;
using System.Text.Json;
using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;
using Microsoft.AspNetCore.WebUtilities;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Backfilling;

internal class Backfiller
{
    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly IRooms rooms;
    private readonly IEventSaver eventSaver;
    private readonly P2pNode p2pNode;

    public Backfiller(ILogger logger, BackfillingServiceContext context, P2pNode p2pNode)
    {
        this.logger = logger;
        identityService = context.IdentityService;
        rooms = context.Rooms;
        eventSaver = context.EventSaver;
        this.p2pNode = p2pNode;
    }

    private async Task<PersistentDataUnit[]> GetMissingEventsAsync(
        string roomId,
        string destination,
        GetMissingEventsRequest request,
        CancellationToken cancellationToken)
    {
        string target = $"/_matrix/federation/v1/get_missing_events/{roomId}";
        var signedRequest = identityService.GetSelfIdentity().SignRequest(
            destination: destination,
            requestMethod: HttpMethods.Post,
            requestTarget: target,
            content: request);
        JsonElement response;
        try
        {
            response = await p2pNode.SendAsync(signedRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error fetching events from {}", destination);
            throw;
        }
        try
        {
            var events = response.GetProperty("events").Deserialize<PersistentDataUnit[]>();
            if (events is null)
            {
                logger.LogWarning("Received null events from {}: {}", destination, response);
                return Array.Empty<PersistentDataUnit>();
            }
            return events;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error deserializing events from {}: {}", destination, response);
            throw;
        }
    }

    // Received events should be ancestors of latest events.
    private static Dictionary<string, PersistentDataUnit> FilterAncestors(
        List<PersistentDataUnit> latestEvents,
        Dictionary<string, PersistentDataUnit> receivedEvents)
    {
        var result = new Dictionary<string, PersistentDataUnit>();
        while (latestEvents.Count > 0)
        {
            var newLatestEvents = new List<PersistentDataUnit>();
            foreach (var pdu in latestEvents)
            {
                foreach (string eventId in pdu.AuthorizationEvents.Union(pdu.PreviousEvents))
                {
                    if (receivedEvents.TryGetValue(eventId, out var receivedEvent))
                    {
                        result[eventId] = receivedEvent;
                        newLatestEvents.Add(receivedEvent);
                    }
                }
            }
            latestEvents = newLatestEvents;
        }
        return result;
    }

    public async Task<PersistentDataUnit[]> GetAllMissingEventsAsync(
        string roomId,
        string destination,
        string[] earliestEventIds,
        PersistentDataUnit[] pdus,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Fetching missing events for {}...", roomId);

        using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
        var receivedEvents = new List<PersistentDataUnit>();
        var receivedEventIds = new HashSet<string>();
        var latestEvents = pdus.ToDictionary(EventHash.GetEventId, x => x);
        var latestEventIds = latestEvents.Keys.ToList();
        var missingEventIds = pdus
            .SelectMany(pdu => pdu.PreviousEvents.Union(pdu.AuthorizationEvents))
            .Distinct()
            .ToArray();
        missingEventIds = await roomEventStore.GetMissingEventIdsAsync(missingEventIds);
        while (latestEventIds.Count > 0 && missingEventIds.Length > 0)
        {
            logger.LogDebug("Latest events count: {}", latestEventIds.Count);
            logger.LogDebug("Received events count: {}", receivedEventIds.Count);

            var request = new GetMissingEventsRequest
            {
                EarliestEvents = earliestEventIds.ToArray(),
                LatestEvents = latestEventIds.ToArray(),
                Limit = 100
            };
            var events = await GetMissingEventsAsync(roomId, destination, request, cancellationToken);
            var newEvents = new Dictionary<string, PersistentDataUnit>();
            foreach (var pdu in events)
            {
                if (!EventHash.VerifyHash(pdu))
                {
                    logger.LogWarning("Event hash not valid: {}", pdu.ToJsonElement());
                }
                string? eventId = EventHash.TryGetEventId(pdu);
                if (eventId is null)
                {
                    logger.LogWarning("Failed getting Event ID: {}", pdu.ToJsonElement());
                }
                else
                {
                    newEvents[eventId] = pdu;
                }
            }
            newEvents = FilterAncestors(latestEvents.Values.ToList(), newEvents);
            if (newEvents.Count == 0)
            {
                logger.LogDebug("Received 0 new event.");
                break;
            }
            foreach (var (eventId, pdu) in newEvents)
            {
                if (receivedEventIds.Add(eventId))
                {
                    receivedEvents.Add(pdu);
                }
            }

            missingEventIds = newEvents.Values
                .SelectMany(x => x.PreviousEvents.Union(x.AuthorizationEvents))
                .Union(missingEventIds)
                .Except(receivedEventIds)
                .ToArray();
            missingEventIds = await roomEventStore.GetMissingEventIdsAsync(missingEventIds);
            if (missingEventIds.Length == 0)
            {
                logger.LogDebug("All missing events are found.");
                break;
            }
            var newLatestEvents = new Dictionary<string, PersistentDataUnit>();
            foreach (var (eventId, pdu) in latestEvents)
            {
                if (pdu.AuthorizationEvents.Any(missingEventIds.Contains)
                    || pdu.PreviousEvents.Any(missingEventIds.Contains))
                {
                    newLatestEvents[eventId] = pdu;
                }
            }
            foreach (var (eventId, pdu) in newEvents)
            {
                if (pdu.AuthorizationEvents.Any(missingEventIds.Contains)
                    || pdu.PreviousEvents.Any(missingEventIds.Contains))
                {
                    newLatestEvents[eventId] = pdu;
                }
            }
            latestEvents = newLatestEvents;
            latestEventIds = latestEvents.Keys.ToList();
        }
        logger.LogDebug("Fetched {} events from {}", receivedEvents.Count, destination);
        return receivedEvents.ToArray();
    }

    public async Task BackfillAsync(PersistentDataUnit[] pdus, CancellationToken cancellationToken)
    {
        if (pdus.Length == 0)
        {
            return;
        }
        string roomId = pdus[0].RoomId;
        var memberPeers = p2pNode.MemberStore.GetMembers(roomId);
        if (memberPeers.Length == 0)
        {
            logger.LogDebug("No member for {} is found", roomId);
            return;
        }

        var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
        var members = new HashSet<string>();
        foreach (var (roomStateKey, content) in snapshot.StateContents)
        {
            if (roomStateKey.EventType == EventTypes.Member)
            {
                var memberEvent = content.Deserialize<MemberEvent>()!;
                if (memberEvent.MemberShip == MembershipStates.Join)
                {
                    members.Add(UserIdentifier.Parse(roomStateKey.StateKey!).Id);
                }
            }
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cts.Token,
            MaxDegreeOfParallelism = 3
        };
        logger.LogDebug("Finding destinations for backfilling {}...", roomId);
        using var destinations = new BlockingCollection<string>();
        var distinctDestinations = new ConcurrentDictionary<string, string>();
        _ = Task.Run(async () =>
        {
            try
            {
                await Parallel.ForEachAsync(memberPeers, parallelOptions, async (peerId, token) =>
                {
                    try
                    {
                        var identity = await p2pNode.GetServerIdentityAsync(peerId, token);
                        if (identity is null)
                        {
                            logger.LogWarning("Identity not found from {}", peerId);
                            p2pNode.MemberStore.RemoveMember(roomId, peerId);
                            return;
                        }
                        if (!members.Contains(identity.Id))
                        {
                            logger.LogWarning("{} is not a member of {}", peerId, roomId);
                            p2pNode.MemberStore.RemoveMember(roomId, peerId);
                            return;
                        }
                        if (distinctDestinations.TryAdd(identity.Id, peerId))
                        {
                            destinations.Add(identity.Id, token);
                        }
                        if (distinctDestinations.Count >= 3)
                        {
                            cts.Cancel();
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Error connecting to peer {}", peerId);
                    }
                });
            }
            finally
            {
                destinations.CompleteAdding();
            }
        }, cancellationToken);
        parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = 3
        };
        await Parallel.ForEachAsync(
            destinations.GetConsumingEnumerable(cancellationToken),
            parallelOptions,
            async (destination, token) =>
        {
            try
            {
                var receivedEvents = await GetAllMissingEventsAsync(
                    roomId,
                    destination,
                    snapshot.LatestEventIds.ToArray(),
                    pdus,
                    token);
                if (receivedEvents.Length > 0)
                {
                    var pduMap = receivedEvents.ToDictionary(pdu => EventHash.GetEventId(pdu), pdu => pdu);
                    using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
                    var receiver = new RoomEventsReceiver(
                        roomId,
                        identityService,
                        roomEventStore,
                        eventSaver,
                        new UnresolvedEventNotifier());
                    var errors = await receiver.ReceiveEventsAsync(receivedEvents.Concat(pdus));
                    foreach (var (eventId, error) in errors)
                    {
                        if (error is not null)
                        {
                            var pdu = pduMap[eventId];
                            logger.LogWarning(
                                "Error receiving event {eventId}: {error}, {pdu}",
                                eventId, error, pdu);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error getting missing events from {}", destination);
            }
        });
    }

    public async Task PullLatestEventsAsync(string roomId, string destination, CancellationToken cancellationToken)
    {
        string target = $"/_matrix/federation/v1/backfill/{roomId}";
        target = QueryHelpers.AddQueryString(target, "limit", 20.ToString());
        var request = identityService.GetSelfIdentity().SignRequest(
            destination: destination,
            requestMethod: HttpMethods.Get,
            requestTarget: target);
        JsonElement response;
        try
        {
            response = await p2pNode.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error fetching events from {}", destination);
            return;
        }
        try
        {
            var events = response.GetProperty("pdus").Deserialize<PersistentDataUnit[]>();
            if (events is null)
            {
                logger.LogWarning("Received null events from {}: {}", destination, response);
                return;
            }
            var newEvents = new Dictionary<string, PersistentDataUnit>();
            foreach (var pdu in events)
            {
                if (!EventHash.VerifyHash(pdu))
                {
                    logger.LogWarning("Event hash not valid: {}", pdu.ToJsonElement());
                }
                string? eventId = EventHash.TryGetEventId(pdu);
                if (eventId is null)
                {
                    logger.LogWarning("Failed getting Event ID: {}", pdu.ToJsonElement());
                }
                else
                {
                    newEvents[eventId] = pdu;
                }
            }
            using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
            var missingEventIds = await roomEventStore.GetMissingEventIdsAsync(newEvents.Keys);
            if (missingEventIds.Length > 0)
            {
                logger.LogDebug("Backfilling {} unresolved events from {}...", missingEventIds.Length, destination);
                var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
                var pdus = missingEventIds.Select(eventId => newEvents[eventId]).ToArray();
                var receivedEvents = await GetAllMissingEventsAsync(
                    roomId,
                    destination,
                    snapshot.LatestEventIds.ToArray(),
                    pdus,
                    cancellationToken);
                if (receivedEvents.Length > 0)
                {
                    var pduMap = receivedEvents.ToDictionary(pdu => EventHash.GetEventId(pdu), pdu => pdu);
                    var receiver = new RoomEventsReceiver(
                        roomId,
                        identityService,
                        roomEventStore,
                        eventSaver,
                        new UnresolvedEventNotifier());
                    var errors = await receiver.ReceiveEventsAsync(receivedEvents.Concat(pdus));
                    foreach (var (eventId, error) in errors)
                    {
                        if (error is not null)
                        {
                            var pdu = pduMap[eventId];
                            logger.LogWarning(
                                "Error receiving event {eventId}: {error}, {pdu}",
                                eventId, error, pdu);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error deserializing events from {}: {}", destination, response);
            return;
        }
    }
}
