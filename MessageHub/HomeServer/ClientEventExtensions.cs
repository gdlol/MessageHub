using MessageHub.ClientServerProtocol;

namespace MessageHub.HomeServer;

public static class ClientEventExtensions
{
    public static ClientEvent ToClientEvent(this ClientEventWithoutRoomID clientEventWithoutRoomID, string roomId)
    {
        ArgumentNullException.ThrowIfNull(clientEventWithoutRoomID);

        return new ClientEvent
        {
            Content = clientEventWithoutRoomID.Content,
            EventId = clientEventWithoutRoomID.EventId,
            OriginServerTimestamp = clientEventWithoutRoomID.OriginServerTimestamp,
            RoomId = roomId,
            Sender = clientEventWithoutRoomID.Sender,
            StateKey = clientEventWithoutRoomID.StateKey,
            EventType = clientEventWithoutRoomID.EventType,
            Unsigned = clientEventWithoutRoomID.Unsigned
        };
    }
}
