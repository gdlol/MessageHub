using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.Dummy.Rooms.Timeline;

public class DummyEventSaver : IEventSaver
{
    private readonly ManualResetEvent locker = new(initialState: true);
    private readonly ILogger logger;
    private readonly IPeerIdentity peerIdentity;

    public DummyEventSaver(ILogger<DummyEventSaver> logger, IPeerIdentity peerIdentity)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(peerIdentity);

        this.logger = logger;
        this.peerIdentity = peerIdentity;
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
            logger.LogInformation("Saving event {eventId}: {pdu}", eventId, pdu);
            var rooms = new DummyRooms();

            bool isAuthorized = true;
            var snapshot = rooms.HasRoom(roomId) ? await rooms.GetRoomSnapshotAsync(roomId) : new RoomSnapshot();
            var authorizer = new EventAuthorizer(snapshot.StateContents);
            if (!authorizer.Authorize(pdu.EventType, pdu.StateKey, UserIdentifier.Parse(pdu.Sender), pdu.Content))
            {
                isAuthorized = false;
                logger.LogWarning(
                    "Event {eventId} not authorized at state {state}",
                    eventId,
                    JsonSerializer.Serialize(snapshot.StateContents));
            }

            await rooms.AddEventAsync(eventId, pdu, states.ToImmutableDictionary());
            var userId = UserIdentifier.FromId(peerIdentity.Id).ToString();

            if (!isAuthorized)
            {
                return;
            }

            var batchStates = DummyTimeline.BatchStates;
            var (joinedRoomIds, leftRoomIds, invites, knocks) = (
                batchStates.JoinedRoomIds,
                batchStates.LeftRoomIds,
                batchStates.Invites,
                batchStates.Knocks);
            if (pdu.EventType == EventTypes.Member && userId == pdu.StateKey)
            {
                var memberEvent = JsonSerializer.Deserialize<MemberEvent>(pdu.Content)!;
                if (memberEvent.MemberShip == MembershipStates.Join)
                {
                    if (!joinedRoomIds.Contains(roomId))
                    {
                        joinedRoomIds = joinedRoomIds.Add(roomId);
                    }
                    if (leftRoomIds.Contains(roomId))
                    {
                        leftRoomIds = leftRoomIds.Remove(roomId);
                    }
                    if (invites.ContainsKey(roomId))
                    {
                        invites = invites.Remove(roomId);
                    }
                }
                else if (memberEvent.MemberShip == MembershipStates.Leave)
                {
                    if (joinedRoomIds.Contains(roomId))
                    {
                        joinedRoomIds = joinedRoomIds.Remove(roomId);
                    }
                    leftRoomIds = leftRoomIds.Add(roomId);
                    if (invites.ContainsKey(roomId))
                    {
                        invites = invites.Remove(roomId);
                    }
                    if (knocks.ContainsKey(roomId))
                    {
                        knocks = knocks.Remove(roomId);
                    }
                }
            }
            DummyTimeline.AddBatch(
                newEventIds: ImmutableDictionary<string, string[]>.Empty.Add(roomId, new[] { eventId }),
                joinedRoomIds: joinedRoomIds,
                leftRoomIds: leftRoomIds,
                invites: invites,
                knocks: knocks);
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
            var rooms = new DummyRooms();

            var newEventIds = new List<string>();
            foreach (string eventId in eventIds)
            {
                var pdu = events[eventId];
                logger.LogInformation("Saving event {eventId}: {pdu}", eventId, pdu);

                var senderId = UserIdentifier.Parse(pdu.Sender);
                var snapshot = rooms.HasRoom(roomId) ? await rooms.GetRoomSnapshotAsync(roomId) : new RoomSnapshot();
                var authorizer = new EventAuthorizer(snapshot.StateContents);
                if (authorizer.Authorize(pdu.EventType, pdu.StateKey, senderId, pdu.Content))
                {
                    newEventIds.Add(eventId);
                }
                else
                {
                    logger.LogWarning(
                        "Event {eventId} not authorized at state {state}",
                        eventId,
                        JsonSerializer.Serialize(snapshot.StateContents));
                }
                await rooms.AddEventAsync(eventId, pdu, states[eventId]);
            }
            {
                var batchStates = DummyTimeline.BatchStates;
                var (joinedRoomIds, leftRoomIds, invites, knocks) = (
                    batchStates.JoinedRoomIds,
                    batchStates.LeftRoomIds,
                    batchStates.Invites,
                    batchStates.Knocks);
                var userId = UserIdentifier.FromId(peerIdentity.Id).ToString();
                var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
                if (snapshot.StateContents.TryGetValue(new RoomStateKey(EventTypes.Member, userId), out var content))
                {
                    var memberEvent = JsonSerializer.Deserialize<MemberEvent>(content)!;
                    if (memberEvent.MemberShip == MembershipStates.Join)
                    {
                        if (!joinedRoomIds.Contains(roomId))
                        {
                            joinedRoomIds = joinedRoomIds.Add(roomId);
                        }
                        if (leftRoomIds.Contains(roomId))
                        {
                            leftRoomIds = leftRoomIds.Remove(roomId);
                        }
                        if (invites.ContainsKey(roomId))
                        {
                            invites = invites.Remove(roomId);
                        }
                    }
                    else if (memberEvent.MemberShip == MembershipStates.Leave)
                    {
                        if (joinedRoomIds.Contains(roomId))
                        {
                            joinedRoomIds = joinedRoomIds.Remove(roomId);
                        }
                        leftRoomIds = leftRoomIds.Add(roomId);
                        if (invites.ContainsKey(roomId))
                        {
                            invites = invites.Remove(roomId);
                        }
                        if (knocks.ContainsKey(roomId))
                        {
                            knocks = knocks.Remove(roomId);
                        }
                    }
                }
                DummyTimeline.AddBatch(
                    newEventIds: ImmutableDictionary<string, string[]>.Empty.Add(roomId, newEventIds.ToArray()),
                    joinedRoomIds: joinedRoomIds,
                    leftRoomIds: leftRoomIds,
                    invites: invites,
                    knocks: knocks);
            }
        }
        finally
        {
            locker.Set();
        }
    }

    public Task SaveInviteAsync(string roomId, IEnumerable<StrippedStateEvent>? states)
    {
        locker.WaitOne();
        try
        {
            var batchStates = DummyTimeline.BatchStates;
            var (joinedRoomIds, leftRoomIds, invites, knocks) = (
                batchStates.JoinedRoomIds,
                batchStates.LeftRoomIds,
                batchStates.Invites,
                batchStates.Knocks);
            invites = invites.SetItem(roomId, states?.ToImmutableList() ?? ImmutableList<StrippedStateEvent>.Empty);
            DummyTimeline.AddBatch(
                newEventIds: ImmutableDictionary<string, string[]>.Empty,
                joinedRoomIds: joinedRoomIds,
                leftRoomIds: leftRoomIds,
                invites: invites,
                knocks: knocks);
            return Task.CompletedTask;
        }
        finally
        {
            locker.Set();
        }
    }

    public Task SaveKnockAsync(string roomId, IEnumerable<StrippedStateEvent>? states)
    {
        locker.WaitOne();
        try
        {
            var batchStates = DummyTimeline.BatchStates;
            var (joinedRoomIds, leftRoomIds, invites, knocks) = (
                batchStates.JoinedRoomIds,
                batchStates.LeftRoomIds,
                batchStates.Invites,
                batchStates.Knocks);
            knocks = knocks.SetItem(roomId, states?.ToImmutableList() ?? ImmutableList<StrippedStateEvent>.Empty);
            DummyTimeline.AddBatch(
                newEventIds: ImmutableDictionary<string, string[]>.Empty,
                joinedRoomIds: joinedRoomIds,
                leftRoomIds: leftRoomIds,
                invites: invites,
                knocks: knocks);
            return Task.CompletedTask;
        }
        finally
        {
            locker.Set();
        }
    }
}
