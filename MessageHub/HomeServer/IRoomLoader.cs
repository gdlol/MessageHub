using MessageHub.ClientServerProtocol;

namespace MessageHub.HomeServer;

public interface ITimelineIterator
{
    ClientEventWithoutRoomID CurrentEvent { get; }
    ValueTask<bool> TryMoveBackwardAsync();
    ClientEventWithoutRoomID[] GetStateEvents();
}

public interface IRoomStates
{
    string BatchId { get; }
    IReadOnlyList<string> InvitedRoomIds { get; }
    IReadOnlyList<string> JoinedRoomIds { get; }
    IReadOnlyList<string> KnockedRoomIds { get; }
    IReadOnlyList<string> LeftRoomIds { get; }
    StrippedStateEvent[] GetStrippedStateEvents(string roomId);
    Task<ITimelineIterator> GetTimelineIteratorAsync(string roomId);
}

public interface IRoomLoader
{
    bool IsEmpty { get; }
    Task<IRoomStates> LoadRoomStatesAsync(Func<string, bool> roomIdFilter, bool includeLeave);
    Task<IReadOnlyDictionary<string, string>> GetRoomEventIds(string? since);
    ValueTask<ClientEventWithoutRoomID[]> GetRoomStateEvents(string roomId, string? sinceEventId);
}
