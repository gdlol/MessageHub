using System.Text.Json;
using MessageHub.ClientServer.Protocol.Events.Room;
using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.Dummy;

public class DummyEventSender
{
    private readonly static HashSet<string> transactionIds = new();

    public Task<(string? eventId, MatrixError? error)> SendStateEventAsync(
        string sender,
        string roomId,
        RoomStateKey stateKey,
        JsonElement content)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(stateKey);
        string? eventId = null;
        MatrixError? error = null;
        var getResult = () => Task.FromResult((eventId, error));
        if (!RoomHistory.RoomStatesList[^1].Rooms.ContainsKey(roomId)
            && stateKey.EventType != EventTypes.Create)
        {
            error = MatrixError.Create(MatrixErrorCode.InvalidRoomState);
            return getResult();
        }
        var pdu = new PersistentDataUnit
        {
            Content = content,
            Origin = DummyHostInfo.Instance.ServerName,
            OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            RoomId = roomId,
            Sender = sender,
            StateKey = stateKey.StateKey,
            EventType = stateKey.EventType
        };
        var membership = stateKey.EventType == EventTypes.Create
            ? RoomMembership.Joined
            : RoomHistory.RoomStatesList[^1].Rooms[roomId].Membership;
        if (stateKey.EventType == EventTypes.Member && stateKey.StateKey == sender)
        {
            try
            {
                var memberEvent = JsonSerializer.Deserialize<MemberEvent>(content);
                membership = memberEvent?.MemberShip switch
                {
                    MembershipStates.Invite => RoomMembership.Invited,
                    MembershipStates.Join => RoomMembership.Joined,
                    MembershipStates.Knock => RoomMembership.Knocked,
                    MembershipStates.Leave => RoomMembership.Left,
                    _ => throw new InvalidOperationException()
                };
            }
            catch (Exception)
            {
                error = MatrixError.Create(MatrixErrorCode.InvalidParameter, $"{nameof(content)}: {content}");
                return getResult();
            }
        }
        EventHash.UpdateHash(pdu);
        RoomHistory.AddEvent(pdu, membership);
        eventId = EventHash.GetEventId(pdu);
        return getResult();
    }

    public Task<(string? eventId, MatrixError? error)> SendMessageEventAsync(
        string sender,
        string roomId,
        string eventType,
        string transactionId,
        JsonElement content)
    {
        string? eventId = null;
        MatrixError? error = null;
        var getResult = () => Task.FromResult((eventId, error));
        if (!RoomHistory.RoomStatesList[^1].Rooms.ContainsKey(roomId))
        {
            error = MatrixError.Create(MatrixErrorCode.InvalidRoomState);
            return getResult();
        }
        var pdu = new PersistentDataUnit
        {
            Content = content,
            Origin = DummyHostInfo.Instance.ServerName,
            OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            RoomId = roomId,
            Sender = sender,
            EventType = eventType,
            Unsigned = JsonSerializer.SerializeToElement(new
            {
                transaction_id = transactionId
            })
        };
        var membership = RoomHistory.RoomStatesList[^1].Rooms[roomId].Membership;
        lock (transactionIds)
        {
            if (transactionIds.Contains(transactionId))
            {
                error = MatrixError.Create(MatrixErrorCode.LimitExceeded);
                return getResult();
            }
            EventHash.UpdateHash(pdu);
            RoomHistory.AddEvent(pdu, membership);
            eventId = EventHash.GetEventId(pdu);
            transactionIds.Add(transactionId);
        }
        return getResult();
    }
}
