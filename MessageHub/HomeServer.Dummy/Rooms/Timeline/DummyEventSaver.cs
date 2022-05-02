using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.Dummy.Rooms.Timeline;

public class DummyEventSaver : IEventSaver
{
    private readonly object locker = new();
    private readonly IPeerIdentity peerIdentity;
    private readonly IEventPublisher eventPublisher;

    public DummyEventSaver(IPeerIdentity peerIdentity, IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.peerIdentity = peerIdentity;
        this.eventPublisher = eventPublisher;
    }

    private async Task InternalSaveAsync(
        string roomId,
        string eventId,
        PersistentDataUnit pdu,
        IReadOnlyDictionary<RoomStateKey, string> states)
    {
        var rooms = new DummyRooms();

        await rooms.AddEventAsync(eventId, pdu, states.ToImmutableDictionary());
        var userId = UserIdentifier.FromId(peerIdentity.Id).ToString();
        if (userId == pdu.Sender)
        {
            await eventPublisher.PublishAsync(pdu);
        }

        // var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
        // var authorizer = new EventAuthorizer(snapshot.StateContents);
        // if (!authorizer.Authorize(pdu.EventType, pdu.StateKey, UserIdentifier.Parse(pdu.Sender), pdu.Content))
        // {
        //     return;
        // }

        var roomStates = DummyTimeline.RoomStates;
        var invitedRooms = roomStates.InvitedRoomIds.ToDictionary(
            x => x,
            x => roomStates.GetStrippedStateEvents(x).ToImmutableList());
        var joinedRooms = roomStates.JoinedRoomIds;
        var knockedRooms = roomStates.KnockedRoomIds.ToDictionary(
            x => x,
            x => roomStates.GetStrippedStateEvents(x).ToImmutableList());
        var leftRooms = roomStates.LeftRoomIds;
        if (pdu.EventType == EventTypes.Member && userId == pdu.StateKey)
        {
            var memberEvent = JsonSerializer.Deserialize<MemberEvent>(pdu.Content)!;
            if (memberEvent.MemberShip == MembershipStates.Join)
            {
                if (!joinedRooms.Contains(roomId))
                {
                    joinedRooms = joinedRooms.Add(roomId);
                }
                if (invitedRooms.ContainsKey(roomId))
                {
                    invitedRooms.Remove(roomId);
                }
            }
            else if (memberEvent.MemberShip == MembershipStates.Leave)
            {
                joinedRooms = joinedRooms.Remove(roomId);
                leftRooms = leftRooms.Add(roomId);
            }
            else
            {
                throw new InvalidOperationException($"{nameof(memberEvent.MemberShip)}: {memberEvent.MemberShip}");
            }
        }
        DummyTimeline.AddBatch(
            newEventIds: ImmutableDictionary<string, string[]>.Empty.Add(roomId, new[] { eventId }),
            invitedRooms: invitedRooms,
            joinedRooms: joinedRooms,
            knockedRooms: knockedRooms,
            leftRooms: leftRooms);
    }

    public async Task SaveAsync(
        string roomId,
        string eventId,
        PersistentDataUnit pdu,
        IReadOnlyDictionary<RoomStateKey, string> states)
    {
        Monitor.Enter(locker);
        try
        {
            await InternalSaveAsync(roomId, eventId, pdu, states);
        }
        finally
        {
            Monitor.Exit(locker);
        }
    }

    public async Task SaveBatchAsync(
        string roomId,
        IReadOnlyList<string> eventIds,
        IReadOnlyDictionary<string, PersistentDataUnit> events,
        IReadOnlyDictionary<string, ImmutableDictionary<RoomStateKey, string>> states)
    {
        Monitor.Enter(locker);
        try
        {
            foreach (string eventId in eventIds)
            {
                await InternalSaveAsync(roomId, eventId, events[eventId], states[eventId]);
            }
        }
        finally
        {
            Monitor.Exit(locker);
        }
    }

    public Task SaveInviteAsync(string roomId, IEnumerable<StrippedStateEvent>? states)
    {
        Monitor.Enter(locker);
        try
        {
            var roomStates = DummyTimeline.RoomStates;
            var invitedRooms = roomStates.InvitedRoomIds.ToDictionary(
                x => x,
                x => roomStates.GetStrippedStateEvents(x).ToImmutableList());
            var knockedRooms = roomStates.KnockedRoomIds.ToDictionary(
                x => x,
                x => roomStates.GetStrippedStateEvents(x).ToImmutableList());
            invitedRooms[roomId] = states?.ToImmutableList() ?? ImmutableList<StrippedStateEvent>.Empty;
            DummyTimeline.AddBatch(
                newEventIds: ImmutableDictionary<string, string[]>.Empty,
                invitedRooms: invitedRooms,
                joinedRooms: roomStates.JoinedRoomIds,
                knockedRooms: knockedRooms,
                leftRooms: roomStates.LeftRoomIds);
            return Task.CompletedTask;
        }
        finally
        {
            Monitor.Exit(locker);
        }
    }

    public Task SaveKnockAsync(string roomId, IEnumerable<StrippedStateEvent>? states)
    {
        Monitor.Enter(locker);
        try
        {
            var roomStates = DummyTimeline.RoomStates;
            var invitedRooms = roomStates.InvitedRoomIds.ToDictionary(
                x => x,
                x => roomStates.GetStrippedStateEvents(x).ToImmutableList());
            var knockedRooms = roomStates.KnockedRoomIds.ToDictionary(
                x => x,
                x => roomStates.GetStrippedStateEvents(x).ToImmutableList());
            knockedRooms[roomId] = states?.ToImmutableList() ?? ImmutableList<StrippedStateEvent>.Empty;
            DummyTimeline.AddBatch(
                newEventIds: ImmutableDictionary<string, string[]>.Empty,
                invitedRooms: invitedRooms,
                joinedRooms: roomStates.JoinedRoomIds,
                knockedRooms: knockedRooms,
                leftRooms: roomStates.LeftRoomIds);
            return Task.CompletedTask;
        }
        finally
        {
            Monitor.Exit(locker);
        }
    }
}
