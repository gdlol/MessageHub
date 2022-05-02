namespace MessageHub.HomeServer.Rooms.Timeline;

public interface ITimelineLoader
{
    bool IsEmpty { get; }
    string CurrentBatchId { get; }
    Task<IRoomStates> LoadRoomStatesAsync(Func<string, bool> roomIdFilter, bool includeLeave);
    Task<IReadOnlyDictionary<string, string>> GetRoomEventIds(string? batchId);
    Task<ITimelineIterator?> GetTimelineIteratorAsync(string roomId, string eventId);
}
