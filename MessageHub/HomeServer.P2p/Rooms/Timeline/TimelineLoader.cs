using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.P2p.Rooms.Timeline;

internal class TimelineLoader : ITimelineLoader
{
    private readonly EventStore eventStore;
    private readonly IStorageProvider storageProvider;

    public TimelineLoader(EventStore eventStore, IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(storageProvider);

        this.eventStore = eventStore;
        this.storageProvider = storageProvider;
    }

    public bool IsEmpty => string.IsNullOrEmpty(eventStore.CurrentBatchId);

    public string CurrentBatchId => eventStore.CurrentBatchId;

    public async Task<BatchStates> LoadBatchStatesAsync(Func<string, bool> roomIdFilter, bool includeLeave)
    {
        string batchId = eventStore.CurrentBatchId;
        using var store = storageProvider.GetEventStore();
        var roomEventIds = await EventStore.GetRoomEventIdsAsync(store, batchId);
        if (roomEventIds is null)
        {
            throw new InvalidOperationException();
        }
        return new BatchStates
        {
            BatchId = batchId,
            JoinedRoomIds = eventStore.JoinedRoomIds,
            LeftRoomIds = eventStore.LeftRoomIds,
            Invites = eventStore.Invites,
            Knocks = eventStore.Knocks,
            RoomEventIds = roomEventIds
        }.Filter(roomIdFilter, includeLeave);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetRoomEventIds(string? batchId)
    {
        batchId ??= eventStore.CurrentBatchId;
        using var store = storageProvider.GetEventStore();
        var roomEventIds = await EventStore.GetRoomEventIdsAsync(store, batchId);
        if (roomEventIds is null)
        {
            throw new InvalidOperationException();
        }
        return roomEventIds;
    }

    public async Task<ITimelineIterator?> GetTimelineIteratorAsync(string roomId, string eventId)
    {
        var store = storageProvider.GetEventStore();
        var record = await EventStore.GetTimelineRecordAsync(store, roomId, eventId);
        if (record is null)
        {
            return null;
        }
        var iterator = new TimelineIterator(store, roomId, eventId);
        return iterator;
    }
}
