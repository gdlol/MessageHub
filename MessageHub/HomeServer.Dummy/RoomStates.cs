using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.ClientServer.Protocol;

namespace MessageHub.HomeServer.Dummy;

internal class RoomStates : IRoomStates
{
    public ImmutableDictionary<string, Room> Rooms { get; }

    public string BatchId { get; }

    public ImmutableList<string> InvitedRoomIds { get; }

    public ImmutableList<string> JoinedRoomIds { get; }

    public ImmutableList<string> KnockedRoomIds { get; }

    public ImmutableList<string> LeftRoomIds { get; }

    IReadOnlyList<string> IRoomStates.InvitedRoomIds => InvitedRoomIds;

    IReadOnlyList<string> IRoomStates.JoinedRoomIds => JoinedRoomIds;

    IReadOnlyList<string> IRoomStates.KnockedRoomIds => KnockedRoomIds;

    IReadOnlyList<string> IRoomStates.LeftRoomIds => LeftRoomIds;

    private RoomStates(ImmutableDictionary<string, Room> rooms, string batchId)
    {
        Rooms = rooms;
        BatchId = batchId;
        var roomIdBuilders = new[]
        {
            RoomMembership.Invited,
            RoomMembership.Joined,
            RoomMembership.Knocked,
            RoomMembership.Left
        }.ToDictionary(key => key, _ => ImmutableList.CreateBuilder<string>());
        foreach (var (roomId, room) in rooms)
        {
            roomIdBuilders[room.Membership].Add(roomId);
        }
        InvitedRoomIds = roomIdBuilders[RoomMembership.Invited].ToImmutable();
        JoinedRoomIds = roomIdBuilders[RoomMembership.Joined].ToImmutable();
        KnockedRoomIds = roomIdBuilders[RoomMembership.Knocked].ToImmutable();
        LeftRoomIds = roomIdBuilders[RoomMembership.Left].ToImmutable();
    }

    public static RoomStates Empty { get; } = new RoomStates(
        ImmutableDictionary<string, Room>.Empty,
        string.Empty);

    public StrippedStateEvent[] GetStrippedStateEvents(string roomId)
    {
        var room = Rooms[roomId];
        var result = new List<StrippedStateEvent>();
        string latestEventId = room.EventIds[^1];
        foreach (var (stateKey, eventId) in room.States[latestEventId])
        {
            string pduJson = room.Events[eventId];
            var pdu = JsonSerializer.Deserialize<PersistentDataUnit>(pduJson)!;
            result.Add(new StrippedStateEvent
            {
                Content = pdu.Content,
                Sender = pdu.Sender,
                StateKey = stateKey.StateKey,
                EventType = stateKey.EventType
            });
        }
        return result.ToArray();
    }

    public Task<ITimelineIterator> GetTimelineIteratorAsync(string roomId)
    {
        var room = Rooms[roomId];
        ITimelineIterator iterator = new TimelineIterator(room);
        return Task.FromResult(iterator);
    }

    public RoomStates Filter(Func<string, bool> roomIdFilter, bool includeLeave)
    {
        var rooms = Rooms
            .Where(x => roomIdFilter(x.Key))
            .Where(x => includeLeave || x.Value.Membership != RoomMembership.Left)
            .ToImmutableDictionary();
        return new RoomStates(rooms, BatchId);
    }

    public RoomStates AddEvent(PersistentDataUnit pdu, RoomMembership membership)
    {
        string eventId = EventHash.GetEventId(pdu);
        var newRoom = Rooms.TryGetValue(pdu.RoomId, out var room)
            ? room.AddEvent(eventId, pdu, membership)
            : Room.Create(eventId, pdu, membership);
        var newRooms = Rooms.SetItem(pdu.RoomId, newRoom);
        return new RoomStates(newRooms, Guid.NewGuid().ToString());
    }
}
