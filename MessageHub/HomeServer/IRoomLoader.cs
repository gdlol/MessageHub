using System.Text.Json;
using MessageHub.ClientServer.Protocol;

namespace MessageHub.HomeServer;

public interface ITimelineIterator
{
    ClientEventWithoutRoomID CurrentEvent { get; }
    ValueTask<bool> TryMoveForwardAsync();
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
    string CurrentBatchId { get; }
    Task<IRoomStates> LoadRoomStatesAsync(Func<string, bool> roomIdFilter, bool includeLeave);
    Task<IReadOnlyDictionary<string, string>> GetRoomEventIds(string? since);
    bool HasRoom(string roomId);
    ValueTask<ClientEventWithoutRoomID[]> GetRoomStateEvents(string roomId, string? sinceEventId);
    Task<ClientEvent?> LoadEventAsync(string roomId, string eventId);
    Task<ClientEvent[]?> LoadRoomMembersAsync(string roomId, string? sinceEventId);
    Task<JsonElement?> LoadStateAsync(string roomId, RoomStateKey stateKey);
    Task<ITimelineIterator?> GetTimelineIteratorAsync(string roomId, string eventId);
}
