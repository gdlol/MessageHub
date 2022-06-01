using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.P2p.Notifiers;
using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.P2p.Rooms.Timeline;

internal sealed class EventSaver : IEventSaver
{
    private readonly ManualResetEvent locker = new(initialState: true);
    private readonly ILogger logger;
    private readonly EventStore eventStore;
    private readonly IStorageProvider storageProvider;
    private readonly IIdentityService identityService;
    private readonly MembershipUpdateNotifier notifier;

    public EventSaver(
        ILogger<EventSaver> logger,
        EventStore eventStore,
        IStorageProvider storageProvider,
        IIdentityService identityService,
        MembershipUpdateNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(storageProvider);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(notifier);

        this.logger = logger;
        this.eventStore = eventStore;
        this.storageProvider = storageProvider;
        this.identityService = identityService;
        this.notifier = notifier;
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
        locker.WaitOne();
        try
        {
            using var store = storageProvider.GetEventStore();
            var newEventStore = eventStore.Update();

            // Event data update.
            logger.LogInformation("Saving event {eventId}: {pdu}", eventId, pdu);
            bool isAuthorized = true;
            var snapshot = await EventStore.GetRoomSnapshotAsync(store, roomId);
            var authorizer = new EventAuthorizer(snapshot.StateContents);
            if (!authorizer.Authorize(pdu.EventType, pdu.StateKey, UserIdentifier.Parse(pdu.Sender), pdu.Content))
            {
                isAuthorized = false;
                logger.LogWarning(
                    "Event {eventId} not authorized at state {state}",
                    eventId,
                    JsonSerializer.Serialize(snapshot.StateContents));
            }
            if (!newEventStore.RoomCreators.ContainsKey(pdu.RoomId))
            {
                if (pdu.EventType != EventTypes.Create)
                {
                    throw new InvalidOperationException($"{nameof(pdu.EventType)}: {pdu.EventType}");
                }
                string creator = pdu.Sender;
                newEventStore = await newEventStore.SetCreatorAsync(store, roomId, creator);
            }
            await EventStore.PutEventAsync(store, roomId, eventId, pdu);
            await EventStore.PutStatesAsync(store, roomId, eventId, states.ToImmutableDictionary());
            using var newRoomEventStore = new RoomEventStore(newEventStore, store, pdu.RoomId, ownsStore: false);
            var newSnapshot = await GetNewSnapshotAsync(newRoomEventStore, snapshot, eventId, pdu);
            await EventStore.PutRoomSnapshotAsync(store, roomId, newSnapshot);

            if (!isAuthorized)
            {
                return;
            }

            // Room state update.
            var userId = UserIdentifier.FromId(identityService.GetSelfIdentity().Id).ToString();
            if (pdu.EventType == EventTypes.Member && userId == pdu.StateKey)
            {
                var memberEvent = JsonSerializer.Deserialize<MemberEvent>(pdu.Content)!;
                if (memberEvent.MemberShip == MembershipStates.Join)
                {
                    if (!newEventStore.JoinedRoomIds.Contains(roomId))
                    {
                        var joinedRoomIds = newEventStore.JoinedRoomIds.Add(roomId);
                        newEventStore = await newEventStore.SetJoinedRoomIdsAsync(store, joinedRoomIds);
                    }
                    if (newEventStore.LeftRoomIds.Contains(roomId))
                    {
                        var leftRoomIds = newEventStore.LeftRoomIds.Remove(roomId);
                        newEventStore = await newEventStore.SetLeftRoomIdsAsync(store, leftRoomIds);
                    }
                    if (newEventStore.Invites.ContainsKey(roomId))
                    {
                        var invites = newEventStore.Invites.Remove(roomId);
                        newEventStore = await newEventStore.SetInvitesAsync(store, invites);
                    }
                }
                else if (memberEvent.MemberShip == MembershipStates.Leave)
                {
                    if (newEventStore.JoinedRoomIds.Contains(roomId))
                    {
                        var joinedRoomIds = newEventStore.JoinedRoomIds.Remove(roomId);
                        newEventStore = await newEventStore.SetJoinedRoomIdsAsync(store, joinedRoomIds);
                    }
                    if (!newEventStore.LeftRoomIds.Contains(roomId))
                    {
                        var leftRoomIds = newEventStore.LeftRoomIds.Add(roomId);
                        newEventStore = await newEventStore.SetLeftRoomIdsAsync(store, leftRoomIds);
                    }
                    if (newEventStore.Invites.ContainsKey(roomId))
                    {
                        var invites = newEventStore.Invites.Remove(roomId);
                        newEventStore = await newEventStore.SetInvitesAsync(store, invites);
                    }
                    if (newEventStore.Knocks.ContainsKey(roomId))
                    {
                        var knocks = newEventStore.Knocks.Remove(roomId);
                        newEventStore = await newEventStore.SetKnocksAsync(store, knocks);
                    }
                }
            }

            // Timeline update.
            string newBatchId = Guid.NewGuid().ToString();
            var roomEventIds = await EventStore.GetRoomEventIdsAsync(store, newEventStore.CurrentBatchId);
            if (roomEventIds is null)
            {
                roomEventIds = ImmutableDictionary<string, string>.Empty;
            }
            if (roomEventIds.TryGetValue(roomId, out string? latestEventId))
            {
                var timelineRecord = await EventStore.GetTimelineRecordAsync(store, roomId, latestEventId);
                if (timelineRecord is null)
                {
                    throw new InvalidOperationException();
                }
                timelineRecord = timelineRecord with { NextEventId = eventId };
                await EventStore.PutTimelineRecordAsync(store, roomId, latestEventId, timelineRecord);
            }
            await EventStore.PutTimelineRecordAsync(store, roomId, eventId, new TimelineRecord(latestEventId, null));
            roomEventIds = roomEventIds.SetItem(roomId, eventId);
            await EventStore.PutRoomEventIdsAsync(store, newBatchId, roomEventIds);
            newEventStore = await newEventStore.SetCurrentBatchIdAsync(store, newBatchId);

            await store.CommitAsync();
            EventStore.Instance = newEventStore;
            if (pdu.EventType == EventTypes.Member)
            {
                var members = new List<string>();
                foreach (var (roomStateKey, content) in newSnapshot.StateContents)
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
                notifier.Notify((pdu.RoomId, members.ToArray()));
            }
        }
        finally
        {
            locker.Set();
        }
    }

    public async Task SaveBatchAsync(
        string roomId,
        IReadOnlyList<string> eventIds,
        IReadOnlyDictionary<string, PersistentDataUnit> events,
        IReadOnlyDictionary<string, ImmutableDictionary<RoomStateKey, string>> states)
    {
        locker.WaitOne();
        try
        {
            using var store = storageProvider.GetEventStore();
            var newEventStore = eventStore.Update();

            // Event data update.
            var newEventIds = new List<string>();
            foreach (string eventId in eventIds)
            {
                var pdu = events[eventId];
                logger.LogInformation("Saving event {eventId}: {pdu}", eventId, pdu);

                var senderId = UserIdentifier.Parse(pdu.Sender);
                var snapshot = await EventStore.GetRoomSnapshotAsync(store, roomId);
                var authorizer = new EventAuthorizer(snapshot.StateContents);
                if (authorizer.Authorize(pdu.EventType, pdu.StateKey, senderId, pdu.Content))
                {
                    newEventIds.Add(eventId);
                    if (!newEventStore.RoomCreators.ContainsKey(pdu.RoomId))
                    {
                        if (pdu.EventType != EventTypes.Create)
                        {
                            throw new InvalidOperationException($"{nameof(pdu.EventType)}: {pdu.EventType}");
                        }
                        string creator = pdu.Sender;
                        newEventStore = await newEventStore.SetCreatorAsync(store, roomId, creator);
                    }
                    await EventStore.PutEventAsync(store, roomId, eventId, pdu);
                    await EventStore.PutStatesAsync(store, roomId, eventId, states[eventId]);
                    using var newRoomEventStore =
                        new RoomEventStore(newEventStore, store, pdu.RoomId, ownsStore: false);
                    var newSnapshot = await GetNewSnapshotAsync(newRoomEventStore, snapshot, eventId, pdu);
                    await EventStore.PutRoomSnapshotAsync(store, roomId, newSnapshot);
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
                return;
            }

            // Room state update.
            {
                var userId = UserIdentifier.FromId(identityService.GetSelfIdentity().Id).ToString();
                var snapshot = await EventStore.GetRoomSnapshotAsync(store, roomId);
                if (snapshot.StateContents.TryGetValue(new RoomStateKey(EventTypes.Member, userId), out var content))
                {
                    var memberEvent = JsonSerializer.Deserialize<MemberEvent>(content)!;
                    if (memberEvent.MemberShip == MembershipStates.Join)
                    {
                        if (!newEventStore.JoinedRoomIds.Contains(roomId))
                        {
                            var joinedRoomIds = newEventStore.JoinedRoomIds.Add(roomId);
                            newEventStore = await newEventStore.SetJoinedRoomIdsAsync(store, joinedRoomIds);
                        }
                        if (newEventStore.LeftRoomIds.Contains(roomId))
                        {
                            var leftRoomIds = newEventStore.LeftRoomIds.Remove(roomId);
                            newEventStore = await newEventStore.SetLeftRoomIdsAsync(store, leftRoomIds);
                        }
                        if (newEventStore.Invites.ContainsKey(roomId))
                        {
                            var invites = newEventStore.Invites.Remove(roomId);
                            newEventStore = await newEventStore.SetInvitesAsync(store, invites);
                        }
                    }
                    else if (memberEvent.MemberShip == MembershipStates.Leave)
                    {
                        if (newEventStore.JoinedRoomIds.Contains(roomId))
                        {
                            var joinedRoomIds = newEventStore.JoinedRoomIds.Remove(roomId);
                            newEventStore = await newEventStore.SetJoinedRoomIdsAsync(store, joinedRoomIds);
                        }
                        if (!newEventStore.LeftRoomIds.Contains(roomId))
                        {
                            var leftRoomIds = newEventStore.LeftRoomIds.Add(roomId);
                            newEventStore = await newEventStore.SetLeftRoomIdsAsync(store, leftRoomIds);
                        }
                        if (newEventStore.Invites.ContainsKey(roomId))
                        {
                            var invites = newEventStore.Invites.Remove(roomId);
                            newEventStore = await newEventStore.SetInvitesAsync(store, invites);
                        }
                        if (newEventStore.Knocks.ContainsKey(roomId))
                        {
                            var knocks = newEventStore.Knocks.Remove(roomId);
                            newEventStore = await newEventStore.SetKnocksAsync(store, knocks);
                        }
                    }
                }
            }

            // Timeline update.
            string newBatchId = Guid.NewGuid().ToString();
            var roomEventIds = await EventStore.GetRoomEventIdsAsync(store, newEventStore.CurrentBatchId);
            if (roomEventIds is null)
            {
                roomEventIds = ImmutableDictionary<string, string>.Empty;
            }
            foreach (string eventId in newEventIds)
            {
                if (roomEventIds.TryGetValue(roomId, out string? latestEventId))
                {
                    var timelineRecord = await EventStore.GetTimelineRecordAsync(store, roomId, latestEventId);
                    if (timelineRecord is null)
                    {
                        throw new InvalidOperationException();
                    }
                    timelineRecord = timelineRecord with { NextEventId = eventId };
                    await EventStore.PutTimelineRecordAsync(store, roomId, latestEventId, timelineRecord);
                }
                var newTimelineRecord = new TimelineRecord(latestEventId, null);
                await EventStore.PutTimelineRecordAsync(store, roomId, eventId, newTimelineRecord);
                roomEventIds = roomEventIds.SetItem(roomId, eventId);
            }
            await EventStore.PutRoomEventIdsAsync(store, newBatchId, roomEventIds);
            newEventStore = await newEventStore.SetCurrentBatchIdAsync(store, newBatchId);

            await store.CommitAsync();
            EventStore.Instance = newEventStore;
            if (events.Values.Any(pdu => pdu.EventType == EventTypes.Member))
            {
                var snapshot = await EventStore.GetRoomSnapshotAsync(store, roomId);
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
                notifier.Notify((roomId, members.ToArray()));
            }
        }
        finally
        {
            locker.Set();
        }
    }

    public async Task SaveInviteAsync(string roomId, IEnumerable<StrippedStateEvent>? states)
    {
        locker.WaitOne();
        try
        {
            using var store = storageProvider.GetEventStore();
            var newEventStore = eventStore.Update();

            var strippedStates = ImmutableList<StrippedStateEvent>.Empty;
            if (states is not null)
            {
                strippedStates = states.ToImmutableList();
            }
            var invites = newEventStore.Invites.SetItem(roomId, strippedStates);
            newEventStore = await newEventStore.SetInvitesAsync(store, invites);

            var roomEventIds = await EventStore.GetRoomEventIdsAsync(store, newEventStore.CurrentBatchId);
            if (roomEventIds is null)
            {
                roomEventIds = ImmutableDictionary<string, string>.Empty;
            }
            string newBatchId = Guid.NewGuid().ToString();
            await EventStore.PutRoomEventIdsAsync(store, newBatchId, roomEventIds);
            newEventStore = await newEventStore.SetCurrentBatchIdAsync(store, newBatchId);

            await store.CommitAsync();
            EventStore.Instance = newEventStore;
        }
        finally
        {
            locker.Set();
        }
    }

    public async Task SaveKnockAsync(string roomId, IEnumerable<StrippedStateEvent>? states)
    {
        locker.WaitOne();
        try
        {
            using var store = storageProvider.GetEventStore();
            var newEventStore = eventStore.Update();

            var strippedStates = ImmutableList<StrippedStateEvent>.Empty;
            if (states is not null)
            {
                strippedStates = states.ToImmutableList();
            }
            var knocks = newEventStore.Knocks.SetItem(roomId, strippedStates);
            newEventStore = await newEventStore.SetKnocksAsync(store, knocks);

            var roomEventIds = await EventStore.GetRoomEventIdsAsync(store, newEventStore.CurrentBatchId);
            if (roomEventIds is null)
            {
                roomEventIds = ImmutableDictionary<string, string>.Empty;
            }
            string newBatchId = Guid.NewGuid().ToString();
            await EventStore.PutRoomEventIdsAsync(store, newBatchId, roomEventIds);
            newEventStore = await newEventStore.SetCurrentBatchIdAsync(store, newBatchId);

            await store.CommitAsync();
            EventStore.Instance = newEventStore;
        }
        finally
        {
            locker.Set();
        }
    }
}
