using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.Rooms.Timeline;

public interface IRoomStates
{
    string BatchId { get; }
    IReadOnlyList<string> InvitedRoomIds { get; }
    IReadOnlyList<string> JoinedRoomIds { get; }
    IReadOnlyList<string> KnockedRoomIds { get; }
    IReadOnlyList<string> LeftRoomIds { get; }
    StrippedStateEvent[] GetStrippedStateEvents(string roomId);
}
