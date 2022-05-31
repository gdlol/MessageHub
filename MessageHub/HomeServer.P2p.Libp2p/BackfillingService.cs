using System.Collections.Concurrent;
using System.Text.Json;
using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.P2p.Libp2p;

internal class BackfillingService
{
    private class Backfiller
    {
        private readonly ILogger logger;
        private readonly IIdentityService identityService;
        private readonly IRooms rooms;
        private readonly MemberStore memberStore;
        private readonly P2pNode p2pNode;
        private readonly IEventSaver eventSaver;

        public Backfiller(
            ILogger logger,
            IIdentityService identityService,
            IRooms rooms,
            MemberStore memberStore,
            P2pNode p2pNode,
            IEventSaver eventSaver)
        {
            this.logger = logger;
            this.identityService = identityService;
            this.rooms = rooms;
            this.memberStore = memberStore;
            this.p2pNode = p2pNode;
            this.eventSaver = eventSaver;
        }

        private async Task<PersistentDataUnit[]> GetMissingEventsAsync(
            string roomId,
            string destination,
            GetMissingEventsParameters parameters,
            CancellationToken cancellationToken)
        {
            string target = $"/_matrix/federation/v1/get_missing_events/{roomId}";
            var request = identityService.GetSelfIdentity().SignRequest(
                destination: destination,
                requestMethod: HttpMethods.Post,
                requestTarget: target,
                content: parameters);
            JsonElement response;
            try
            {
                response = await p2pNode.SendAsync(request, cancellationToken);
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
                    logger.LogDebug("Received null events from {}: {}", destination, response);
                    return Array.Empty<PersistentDataUnit>();
                }
                return events;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error deserializing events from {}: {}", destination, response);
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


                var parameters = new GetMissingEventsParameters
                {
                    EarliestEvents = earliestEventIds.ToArray(),
                    LatestEvents = latestEventIds.ToArray(),
                    Limit = 100
                };
                var events = await GetMissingEventsAsync(roomId, destination, parameters, cancellationToken);
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
            var result = receivedEvents.Concat(pdus).ToArray();
            logger.LogDebug("Fetched {} events from {}", result.Length, destination);
            return result;
        }

        public async Task BackfillAsync(PersistentDataUnit[] pdus, CancellationToken cancellationToken)
        {
            if (pdus.Length == 0)
            {
                return;
            }
            string roomId = pdus[0].RoomId;
            var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
            var members = new HashSet<string>();
            foreach (var (roomStateKey, content) in snapshot.StateContents)
            {
                if (roomStateKey.EventType == EventTypes.Member)
                {
                    var memberEvent = content.Deserialize<MemberEvent>()!;
                    if (memberEvent.MemberShip == MembershipStates.Join)
                    {
                        members.Add(UserIdentifier.Parse(roomStateKey.StateKey!).PeerId);
                    }
                }
            }

            var memberPeers = memberStore.GetMembers(roomId);
            if (memberPeers.Length == 0)
            {
                logger.LogDebug("No member for {} is found", roomId);
                return;
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
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }
                        try
                        {
                            var identity = await p2pNode.GetServerIdentityAsync(peerId, token);
                            if (identity is null)
                            {
                                logger.LogDebug("Identity not found from {}", peerId);
                                memberStore.RemoveMember(roomId, peerId);
                                return;
                            }
                            if (!members.Contains(identity.Id))
                            {
                                logger.LogDebug("{} is not a member of {}", peerId, roomId);
                                memberStore.RemoveMember(roomId, peerId);
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
            var pduMap = pdus.ToDictionary(pdu => EventHash.GetEventId(pdu), pdu => pdu);
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
                    var events = await GetAllMissingEventsAsync(
                        roomId,
                        destination,
                        snapshot.LatestEventIds.ToArray(),
                        pdus,
                        token);
                    if (events.Length > 0)
                    {
                        using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
                        var receiver = new RoomEventsReceiver(
                            roomId,
                            identityService,
                            roomEventStore,
                            eventSaver,
                            new UnresolvedEventNotifier());
                        var errors = await receiver.ReceiveEventsAsync(events);
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
    }

    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly IRooms rooms;
    private readonly ITimelineLoader timelineLoader;
    private readonly IEventSaver eventSaver;
    private readonly UnresolvedEventNotifier notifier;
    private CancellationTokenSource? cts;
    private BlockingCollection<PersistentDataUnit[]>? pdusQueue;
    private readonly EventHandler<PersistentDataUnit[]>? onNotify;

    public BackfillingService(
        ILogger<BackfillingService> logger,
        IIdentityService identityService,
        IRooms rooms,
        ITimelineLoader timelineLoader,
        IEventSaver eventSaver,
        UnresolvedEventNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(eventSaver);
        ArgumentNullException.ThrowIfNull(notifier);

        this.logger = logger;
        this.identityService = identityService;
        this.rooms = rooms;
        this.timelineLoader = timelineLoader;
        this.eventSaver = eventSaver;
        this.notifier = notifier;
        onNotify = (_, pdus) => pdusQueue?.TryAdd(pdus);
    }

    public void Start(MemberStore memberStore, P2pNode p2pNode, PubSubService pubsubService)
    {
        if (cts is not null)
        {
            throw new InvalidOperationException();
        }
        logger.LogDebug("Starting backfilling service.");
        cts = new CancellationTokenSource();
        var token = cts.Token;
        pdusQueue = new BlockingCollection<PersistentDataUnit[]>(boundedCapacity: 1024);
        notifier.OnNotify += onNotify;
        Task.Run(async () =>
        {
            try
            {
                var backfiller = new Backfiller(
                    logger,
                    identityService,
                    rooms,
                    memberStore,
                    p2pNode,
                    eventSaver);
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = token,
                    MaxDegreeOfParallelism = 3
                };
                await Parallel.ForEachAsync(pdusQueue.GetConsumingEnumerable(), parallelOptions, async (pdus, token) =>
                {
                    try
                    {
                        await backfiller.BackfillAsync(pdus, token);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Error backfilling pdus for {}", pdus.FirstOrDefault()?.RoomId);
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error running backfilling service");
            }
            finally
            {
                logger.LogDebug("Exiting backfilling service.");
            }
        });


        // Periodically advertise latest events
        Task.Run(async () =>
        {
            // Update room memberships.
            try
            {
                var identity = identityService.GetSelfIdentity();
                await Task.Delay(TimeSpan.FromSeconds(3), token);
                while (true)
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        var batchStates = await timelineLoader.LoadBatchStatesAsync(_ => true, includeLeave: false);
                        foreach (string roomId in batchStates.JoinedRoomIds)
                        {
                            var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
                            if (batchStates.RoomEventIds.TryGetValue(roomId, out string? latestEventId))
                            {
                                var pdu = await roomEventStore.LoadEventAsync(latestEventId);
                                string txnId = Guid.NewGuid().ToString();
                                var parameters = new PushMessagesRequest
                                {
                                    Origin = identity.Id,
                                    OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                    Pdus = new[] { pdu }
                                };
                                var request = identity.SignRequest(
                                    destination: roomId,
                                    requestMethod: HttpMethods.Put,
                                    requestTarget: $"/_matrix/federation/v1/send/{txnId}",
                                    content: parameters);
                                pubsubService.Publish(roomId, JsonSerializer.SerializeToElement(request));
                            }
                            else
                            {
                                logger.LogWarning("Latest event id not found for room {}", roomId);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Error publishing event.");
                    }
                    finally
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1));
                    }
                }
            }
            finally
            {
                logger.LogDebug("Stop advertising latest events.");
            }
        });
    }

    public void Stop()
    {
        if (cts is null)
        {
            return;
        }
        logger.LogDebug("Stopping backfilling service.");
        notifier.OnNotify -= onNotify;
        pdusQueue?.CompleteAdding();
        pdusQueue?.Dispose();
        pdusQueue = null;
        cts.Cancel();
        cts.Dispose();
        cts = null;
    }
}
