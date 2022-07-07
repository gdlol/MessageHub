using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.P2p.Notifiers;
using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.P2p.Rooms.Timeline;

internal sealed class EventSaver : IEventSaver
{
    private readonly ILogger logger;
    private readonly EventStore eventStore;
    private readonly IIdentityService identityService;
    private readonly TimelineUpdateNotifier timelineUpdateNotifier;
    private readonly MembershipUpdateNotifier membershipUpdateNotifier;

    public EventSaver(
        ILogger<EventSaver> logger,
        EventStore eventStore,
        IIdentityService identityService,
        TimelineUpdateNotifier timelineUpdateNotifier,
        MembershipUpdateNotifier membershipUpdateNotifier)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(timelineUpdateNotifier);
        ArgumentNullException.ThrowIfNull(membershipUpdateNotifier);

        this.logger = logger;
        this.eventStore = eventStore;
        this.identityService = identityService;
        this.timelineUpdateNotifier = timelineUpdateNotifier;
        this.membershipUpdateNotifier = membershipUpdateNotifier;
    }

    private static async ValueTask<RoomSnapshot> GetNewSnapshotAsync(
        RoomEventStore roomEventStore,
        RoomSnapshot snapshot,
        string eventId,
        PersistentDataUnit pdu)
    {
        var stateResolver = new RoomStateResolver(roomEventStore);
        var latestEventIds = snapshot.LatestEventIds.Except(pdu.PreviousEvents).Union(new[] { eventId });
        var newStates = await stateResolver.ResolveStateAsync(latestEventIds);
        var pdus = new List<PersistentDataUnit>();
        var stateContents = new Dictionary<RoomStateKey, JsonElement>();
        foreach (string latestEventId in latestEventIds)
        {
            var latestEvent = await roomEventStore.LoadEventAsync(latestEventId);
            pdus.Add(latestEvent);
        }
        foreach (string stateEventId in newStates.Values)
        {
            var stateEvent = await roomEventStore.LoadEventAsync(stateEventId);
            stateContents[new RoomStateKey(stateEvent.EventType, stateEvent.StateKey!)] = stateEvent.Content;
        }
        var newSnapshot = new RoomSnapshot
        {
            LatestEventIds = latestEventIds.ToImmutableList(),
            GraphDepth = pdus.Select(x => x.Depth).Max(),
            States = newStates,
            StateContents = stateContents.ToImmutableDictionary()
        };
        return newSnapshot;
    }

    public async Task SaveAsync(
        string roomId,
        string eventId,
        PersistentDataUnit pdu,
        IReadOnlyDictionary<RoomStateKey, string> states)
    {
        var sessionWithWriteLock = eventStore.GetSessionWithWriteLock();
        using var session = sessionWithWriteLock.session;
        using var writeLock = sessionWithWriteLock.writeLock;

        // Event data update.
        logger.LogDebug("Saving event {eventId}: {pdu}", eventId, pdu);
        if (await session.GetEventAsync(roomId, eventId) is not null)
        {
            logger.LogDebug("Event {} already exists.", eventId);
            return;
        }

        bool isAuthorized = true;
        var snapshot = await session.GetRoomSnapshotAsync(roomId);
        var authorizer = new EventAuthorizer(snapshot.StateContents);
        if (!authorizer.Authorize(pdu.EventType, pdu.StateKey, UserIdentifier.Parse(pdu.Sender), pdu.Content))
        {
            isAuthorized = false;
            logger.LogWarning(
                "Event {eventId} not authorized at state {state}",
                eventId,
                JsonSerializer.Serialize(snapshot.StateContents));
        }
        if (!session.State.RoomCreators.ContainsKey(pdu.RoomId))
        {
            if (pdu.EventType != EventTypes.Create)
            {
                throw new InvalidOperationException($"{nameof(pdu.EventType)}: {pdu.EventType}");
            }
            string creator = pdu.Sender;
            var roomCreators = session.State.RoomCreators.Add(roomId, creator);
            await session.SetRoomCreatorsAsync(roomCreators);
        }
        await session.PutEventAsync(roomId, eventId, pdu);
        await session.PutStatesAsync(roomId, eventId, states.ToImmutableDictionary());

        if (!isAuthorized)
        {
            await writeLock.CommitAndReleaseAsync(session.State);
            return;
        }

        using var newRoomEventStore = new RoomEventStore(session, pdu.RoomId, ownsSession: false);
        snapshot = await GetNewSnapshotAsync(newRoomEventStore, snapshot, eventId, pdu);
        await session.PutRoomSnapshotAsync(roomId, snapshot);

        // Room state update.
        var userId = UserIdentifier.FromId(identityService.GetSelfIdentity().Id).ToString();
        if (pdu.EventType == EventTypes.Member && userId == pdu.StateKey)
        {
            var memberEvent = JsonSerializer.Deserialize<MemberEvent>(pdu.Content)!;
            if (memberEvent.MemberShip == MembershipStates.Join)
            {
                if (!session.State.JoinedRoomIds.Contains(roomId))
                {
                    var joinedRoomIds = session.State.JoinedRoomIds.Add(roomId);
                    await session.SetJoinedRoomIdsAsync(joinedRoomIds);
                }
                if (session.State.LeftRoomIds.Contains(roomId))
                {
                    var leftRoomIds = session.State.LeftRoomIds.Remove(roomId);
                    await session.SetLeftRoomIdsAsync(leftRoomIds);
                }
                if (session.State.Invites.ContainsKey(roomId))
                {
                    var invites = session.State.Invites.Remove(roomId);
                    await session.SetInvitesAsync(invites);
                }
            }
            else if (memberEvent.MemberShip == MembershipStates.Leave)
            {
                if (session.State.JoinedRoomIds.Contains(roomId))
                {
                    var joinedRoomIds = session.State.JoinedRoomIds.Remove(roomId);
                    await session.SetJoinedRoomIdsAsync(joinedRoomIds);
                }
                if (!session.State.LeftRoomIds.Contains(roomId))
                {
                    var leftRoomIds = session.State.LeftRoomIds.Add(roomId);
                    await session.SetLeftRoomIdsAsync(leftRoomIds);
                }
                if (session.State.Invites.ContainsKey(roomId))
                {
                    var invites = session.State.Invites.Remove(roomId);
                    await session.SetInvitesAsync(invites);
                }
                if (session.State.Knocks.ContainsKey(roomId))
                {
                    var knocks = session.State.Knocks.Remove(roomId);
                    await session.SetKnocksAsync(knocks);
                }
            }
        }

        // Timeline update.
        string newBatchId = Guid.NewGuid().ToString();
        var roomEventIds = await session.GetRoomEventIdsAsync(session.State.CurrentBatchId);
        if (roomEventIds is null)
        {
            roomEventIds = ImmutableDictionary<string, string>.Empty;
        }
        if (roomEventIds.TryGetValue(roomId, out string? latestEventId))
        {
            var timelineRecord = await session.GetTimelineRecordAsync(roomId, latestEventId);
            if (timelineRecord is null)
            {
                throw new InvalidOperationException();
            }
            timelineRecord = timelineRecord with { NextEventId = eventId };
            await session.PutTimelineRecordAsync(roomId, latestEventId, timelineRecord);
        }
        await session.PutTimelineRecordAsync(roomId, eventId, new TimelineRecord(latestEventId, null));
        roomEventIds = roomEventIds.SetItem(roomId, eventId);
        await session.PutRoomEventIdsAsync(newBatchId, roomEventIds);
        await session.SetCurrentBatchIdAsync(newBatchId);

        await writeLock.CommitAndReleaseAsync(session.State);
        timelineUpdateNotifier.Notify();

        // Notify membership update.
        if (pdu.EventType == EventTypes.Member)
        {
            var members = new List<string>();
            foreach (var (roomStateKey, content) in snapshot.StateContents)
            {
                if (roomStateKey.EventType != EventTypes.Member)
                {
                    continue;
                }
                var memberEvent = JsonSerializer.Deserialize<MemberEvent>(content)!;
                if (memberEvent.MemberShip == MembershipStates.Join)
                {
                    var userIdentifier = UserIdentifier.Parse(roomStateKey.StateKey);
                    members.Add(userIdentifier.Id);
                }
            }
            membershipUpdateNotifier.Notify(new(pdu.RoomId, members.ToArray()));
        }
    }

    public async Task SaveBatchAsync(
        string roomId,
        IReadOnlyList<string> eventIds,
        IReadOnlyDictionary<string, PersistentDataUnit> events,
        IReadOnlyDictionary<string, ImmutableDictionary<RoomStateKey, string>> states)
    {
        var sessionWithWriteLock = eventStore.GetSessionWithWriteLock();
        using var session = sessionWithWriteLock.session;
        using var writeLock = sessionWithWriteLock.writeLock;

        // Event data update.
        var newEventIds = new List<string>();
        var snapshot = await session.GetRoomSnapshotAsync(roomId);
        foreach (string eventId in eventIds)
        {
            var pdu = events[eventId];
            logger.LogDebug("Saving event {eventId}: {pdu}", eventId, pdu);
            if (await session.GetEventAsync(roomId, eventId) is not null)
            {
                logger.LogDebug("Event {} already exists.", eventId);
                continue;
            }

            if (!session.State.RoomCreators.ContainsKey(pdu.RoomId))
            {
                if (pdu.EventType != EventTypes.Create)
                {
                    throw new InvalidOperationException($"{nameof(pdu.EventType)}: {pdu.EventType}");
                }
                string creator = pdu.Sender;
                var roomCreators = session.State.RoomCreators.Add(roomId, creator);
                await session.SetRoomCreatorsAsync(roomCreators);
            }
            await session.PutEventAsync(roomId, eventId, pdu);
            await session.PutStatesAsync(roomId, eventId, states[eventId]);
            var senderId = UserIdentifier.Parse(pdu.Sender);
            var authorizer = new EventAuthorizer(snapshot.StateContents);
            if (authorizer.Authorize(pdu.EventType, pdu.StateKey, senderId, pdu.Content))
            {
                newEventIds.Add(eventId);
                using var newRoomEventStore = new RoomEventStore(session, pdu.RoomId, ownsSession: false);
                snapshot = await GetNewSnapshotAsync(newRoomEventStore, snapshot, eventId, pdu);
                await session.PutRoomSnapshotAsync(roomId, snapshot);
            }
            else
            {
                logger.LogWarning(
                    "Event {eventId} not authorized at state {state}",
                    eventId,
                    JsonSerializer.Serialize(snapshot.StateContents));
            }
        }
        if (newEventIds.Count == 0)
        {
            await writeLock.CommitAndReleaseAsync(session.State);
            return;
        }

        // Room state update.
        {
            var userId = UserIdentifier.FromId(identityService.GetSelfIdentity().Id).ToString();
            if (snapshot.StateContents.TryGetValue(new RoomStateKey(EventTypes.Member, userId), out var content))
            {
                var memberEvent = JsonSerializer.Deserialize<MemberEvent>(content)!;
                if (memberEvent.MemberShip == MembershipStates.Join)
                {
                    if (!session.State.JoinedRoomIds.Contains(roomId))
                    {
                        var joinedRoomIds = session.State.JoinedRoomIds.Add(roomId);
                        await session.SetJoinedRoomIdsAsync(joinedRoomIds);
                    }
                    if (session.State.LeftRoomIds.Contains(roomId))
                    {
                        var leftRoomIds = session.State.LeftRoomIds.Remove(roomId);
                        await session.SetLeftRoomIdsAsync(leftRoomIds);
                    }
                    if (session.State.Invites.ContainsKey(roomId))
                    {
                        var invites = session.State.Invites.Remove(roomId);
                        await session.SetInvitesAsync(invites);
                    }
                }
                else if (memberEvent.MemberShip == MembershipStates.Leave)
                {
                    if (session.State.JoinedRoomIds.Contains(roomId))
                    {
                        var joinedRoomIds = session.State.JoinedRoomIds.Remove(roomId);
                        await session.SetJoinedRoomIdsAsync(joinedRoomIds);
                    }
                    if (!session.State.LeftRoomIds.Contains(roomId))
                    {
                        var leftRoomIds = session.State.LeftRoomIds.Add(roomId);
                        await session.SetLeftRoomIdsAsync(leftRoomIds);
                    }
                    if (session.State.Invites.ContainsKey(roomId))
                    {
                        var invites = session.State.Invites.Remove(roomId);
                        await session.SetInvitesAsync(invites);
                    }
                    if (session.State.Knocks.ContainsKey(roomId))
                    {
                        var knocks = session.State.Knocks.Remove(roomId);
                        await session.SetKnocksAsync(knocks);
                    }
                }
            }
        }

        // Timeline update.
        string newBatchId = Guid.NewGuid().ToString();
        var roomEventIds = await session.GetRoomEventIdsAsync(session.State.CurrentBatchId);
        if (roomEventIds is null)
        {
            roomEventIds = ImmutableDictionary<string, string>.Empty;
        }
        foreach (string eventId in newEventIds)
        {
            if (roomEventIds.TryGetValue(roomId, out string? latestEventId))
            {
                var timelineRecord = await session.GetTimelineRecordAsync(roomId, latestEventId);
                if (timelineRecord is null)
                {
                    throw new InvalidOperationException();
                }
                timelineRecord = timelineRecord with { NextEventId = eventId };
                await session.PutTimelineRecordAsync(roomId, latestEventId, timelineRecord);
            }
            var newTimelineRecord = new TimelineRecord(latestEventId, null);
            await session.PutTimelineRecordAsync(roomId, eventId, newTimelineRecord);
            roomEventIds = roomEventIds.SetItem(roomId, eventId);
        }
        await session.PutRoomEventIdsAsync(newBatchId, roomEventIds);
        await session.SetCurrentBatchIdAsync(newBatchId);

        await writeLock.CommitAndReleaseAsync(session.State);
        timelineUpdateNotifier.Notify();

        // Notify membership updates.
        if (events.Values.Any(pdu => pdu.EventType == EventTypes.Member))
        {
            var members = new List<string>();
            foreach (var (roomStateKey, content) in snapshot.StateContents)
            {
                if (roomStateKey.EventType != EventTypes.Member)
                {
                    continue;
                }
                var memberEvent = JsonSerializer.Deserialize<MemberEvent>(content)!;
                if (memberEvent.MemberShip == MembershipStates.Join)
                {
                    var userIdentifier = UserIdentifier.Parse(roomStateKey.StateKey);
                    members.Add(userIdentifier.Id);
                }
            }
            membershipUpdateNotifier.Notify(new(roomId, members.ToArray()));
        }
    }

    private static async ValueTask SaveNewBatchAsync(EventStoreSession session)
    {
        var roomEventIds = await session.GetRoomEventIdsAsync(session.State.CurrentBatchId);
        if (roomEventIds is null)
        {
            roomEventIds = ImmutableDictionary<string, string>.Empty;
        }
        string newBatchId = Guid.NewGuid().ToString();
        await session.PutRoomEventIdsAsync(newBatchId, roomEventIds);
        await session.SetCurrentBatchIdAsync(newBatchId);
    }

    public async Task SaveInviteAsync(string roomId, IEnumerable<StrippedStateEvent>? states)
    {
        var sessionWithWriteLock = eventStore.GetSessionWithWriteLock();
        using var session = sessionWithWriteLock.session;
        using var writeLock = sessionWithWriteLock.writeLock;

        var strippedStates = ImmutableList<StrippedStateEvent>.Empty;
        if (states is not null)
        {
            strippedStates = states.ToImmutableList();
        }
        var invites = session.State.Invites.SetItem(roomId, strippedStates);
        await session.SetInvitesAsync(invites);

        await SaveNewBatchAsync(session);
        await writeLock.CommitAndReleaseAsync(session.State);
        timelineUpdateNotifier.Notify();
    }

    public async Task RejectInviteAsync(string roomId)
    {
        var sessionWithWriteLock = eventStore.GetSessionWithWriteLock();
        using var session = sessionWithWriteLock.session;
        using var writeLock = sessionWithWriteLock.writeLock;

        bool foundInvite = false;
        if (session.State.Invites.TryGetValue(roomId, out var stateEvents))
        {
            var identity = identityService.GetSelfIdentity();
            var userId = UserIdentifier.FromId(identity.Id).ToString();
            var newStateEvents = new List<StrippedStateEvent>();
            foreach (var stateEvent in stateEvents)
            {
                if (stateEvent.EventType == EventTypes.Member && stateEvent.StateKey == userId)
                {
                    if (foundInvite)
                    {
                        logger.LogWarning("Multiple member event encountered.");
                        continue;
                    }
                    foundInvite = true;
                    newStateEvents.Add(new StrippedStateEvent
                    {
                        Content = JsonSerializer.SerializeToElement(new MemberEvent
                        {
                            MemberShip = MembershipStates.Leave
                        }),
                        Sender = userId,
                        StateKey = userId,
                        EventType = EventTypes.Member
                    });
                }
                else
                {
                    newStateEvents.Add(stateEvent);
                }
            }
            var invites = session.State.Invites.SetItem(roomId, newStateEvents.ToImmutableList());
            await session.SetInvitesAsync(invites);
        }
        if (!foundInvite)
        {
            logger.LogWarning("Invite not found.");
            return;
        }

        await SaveNewBatchAsync(session);
        await writeLock.CommitAndReleaseAsync(session.State);
        timelineUpdateNotifier.Notify();
    }

    public async Task SaveKnockAsync(string roomId, IEnumerable<StrippedStateEvent>? states)
    {
        var sessionWithWriteLock = eventStore.GetSessionWithWriteLock();
        using var session = sessionWithWriteLock.session;
        using var writeLock = sessionWithWriteLock.writeLock;

        var strippedStates = ImmutableList<StrippedStateEvent>.Empty;
        if (states is not null)
        {
            strippedStates = states.ToImmutableList();
        }
        var knocks = session.State.Knocks.SetItem(roomId, strippedStates);
        await session.SetKnocksAsync(knocks);

        await SaveNewBatchAsync(session);
        await writeLock.CommitAndReleaseAsync(session.State);
        timelineUpdateNotifier.Notify();
    }

    public async Task RetractKnockAsync(string roomId)
    {
        var sessionWithWriteLock = eventStore.GetSessionWithWriteLock();
        using var session = sessionWithWriteLock.session;
        using var writeLock = sessionWithWriteLock.writeLock;

        bool foundKnock = false;
        if (session.State.Knocks.TryGetValue(roomId, out var stateEvents))
        {
            var identity = identityService.GetSelfIdentity();
            var userId = UserIdentifier.FromId(identity.Id).ToString();
            var newStateEvents = new List<StrippedStateEvent>();
            foreach (var stateEvent in stateEvents)
            {
                if (stateEvent.EventType == EventTypes.Member && stateEvent.StateKey == userId)
                {
                    if (foundKnock)
                    {
                        logger.LogWarning("Multiple member event encountered.");
                        continue;
                    }
                    foundKnock = true;
                    newStateEvents.Add(new StrippedStateEvent
                    {
                        Content = JsonSerializer.SerializeToElement(new MemberEvent
                        {
                            MemberShip = MembershipStates.Leave
                        }),
                        Sender = userId,
                        StateKey = userId,
                        EventType = EventTypes.Member
                    });
                }
                else
                {
                    newStateEvents.Add(stateEvent);
                }
            }
            var knocks = session.State.Knocks.SetItem(roomId, newStateEvents.ToImmutableList());
            await session.SetKnocksAsync(knocks);
        }
        if (!foundKnock)
        {
            logger.LogWarning("Knock not found.");
            return;
        }

        await SaveNewBatchAsync(session);
        await writeLock.CommitAndReleaseAsync(session.State);
        timelineUpdateNotifier.Notify();
    }

    public async Task ForgetAsync(string roomId)
    {
        var sessionWithWriteLock = eventStore.GetSessionWithWriteLock();
        using var session = sessionWithWriteLock.session;
        using var writeLock = sessionWithWriteLock.writeLock;

        if (!session.State.LeftRoomIds.Contains(roomId))
        {
            logger.LogWarning("RoomId not found in left rooms: {}", roomId);
            return;
        }
        var leftRoomIds = session.State.LeftRoomIds.Remove(roomId);
        await session.SetLeftRoomIdsAsync(leftRoomIds);

        await SaveNewBatchAsync(session);
        await writeLock.CommitAndReleaseAsync(session.State);
        timelineUpdateNotifier.Notify();
    }
}
