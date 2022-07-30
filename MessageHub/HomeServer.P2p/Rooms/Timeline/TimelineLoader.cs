using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.P2p.Rooms.Timeline;

internal class TimelineLoader : ITimelineLoader
{
    private readonly EventStore eventStore;

    public TimelineLoader(EventStore eventStore)
    {
        ArgumentNullException.ThrowIfNull(eventStore);

        this.eventStore = eventStore;
    }

    public string CurrentBatchId => eventStore.LoadState().CurrentBatchId;

    public bool IsEmpty => CurrentBatchId == EventStoreState.EmptyBatchId;

    public async Task<BatchStates> LoadBatchStatesAsync(Func<string, bool> roomIdFilter, bool includeLeave)
    {
        using var session = eventStore.GetReadOnlySession();
        var roomEventIds = await session.GetRoomEventIdsAsync(session.State.CurrentBatchId);
        if (roomEventIds is null)
        {
            throw new InvalidOperationException();
        }
        return new BatchStates
        {
            BatchId = session.State.CurrentBatchId,
            JoinedRoomIds = session.State.JoinedRoomIds,
            LeftRoomIds = session.State.LeftRoomIds,
            Invites = session.State.Invites,
            Knocks = session.State.Knocks,
            RoomEventIds = roomEventIds
        }.Filter(roomIdFilter, includeLeave);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetRoomEventIdsAsync(string? batchId)
    {
        using var session = eventStore.GetReadOnlySession();
        batchId ??= session.State.CurrentBatchId;
        var roomEventIds = await session.GetRoomEventIdsAsync(batchId);
        if (roomEventIds is null)
        {
            throw new InvalidOperationException();
        }
        return roomEventIds;
    }

    public async Task<ITimelineIterator?> GetTimelineIteratorAsync(string roomId, string eventId)
    {
        using var session = eventStore.GetReadOnlySession();
        var record = await session.GetTimelineRecordAsync(roomId, eventId);
        if (record is null)
        {
            return null;
        }
        var iterator = new TimelineIterator(eventStore.GetReadOnlySession(), roomId, eventId);
        return iterator;
    }
}
